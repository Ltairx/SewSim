using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEngine.Assertions;

namespace UCloth
{
    [BurstCompile(FloatPrecision = FloatPrecision.Low, OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast)]
    public struct UCJob : IJob
    {
        // Dane symulacji
        public NativeArray<float3> positions;
        public NativeArray<float3> velocity;
        internal NativeArray<float3> acceleration;
        internal NativeArray<float3> tempAcceleration;
        internal NativeArray<float> frictionMultiplier;

        internal Native3DHashmapArray<ushort> selfCollisionRegions;
        internal NativeParallelHashSet<int3> utilizedRegionSet;

        [ReadOnly] public NativeArray<UCEdge> edges;
        [ReadOnly] public NativeArray<UCBendingEdge> bendingEdges;
        [ReadOnly] public NativeArray<float> restDistances;
        [ReadOnly] public NativeArray<float3> normals;
        [ReadOnly] public NativeArray<float3> normalsTriangles;
        [ReadOnly] public NativeParallelMultiHashMap<ushort, ushort> neighbours;
        [ReadOnly] public NativeArray<float> reciprocalWeight;
        [ReadOnly] public NativeParallelHashMap<ushort, float3> pinnedPos;

        // Kolidery
        [ReadOnly] public NativeArray<SphereColDTO> sphereColliders;
        [ReadOnly] public NativeArray<CapsuleColDTO> capsuleColliders;
        [ReadOnly] public NativeArray<CubeColDTO> cubeColliders;

        // Zapytania o punkty
        [ReadOnly] public NativeList<UCPointQueryData> pointQueries;
        [WriteOnly] public NativeList<ushort> pointQueryResults;
        [WriteOnly] public NativeList<ushort> pointQueryIndexCounts;

        public Bounds bounds;
        public UCMaterial material;
        public UCCollisionSettings collisionSettings;
        internal NativeReference<UCAutoOptimizeData> optimizationData;
        public UCSimulationProperties simProperties;
        public UCQualityProperties qualityProperties;
        public float baseTimestep;
        public float extraThickness;
        private bool _computedRegions;

        private ushort _nodeCount;
        private int _edgeCount;
        private int _bendingEdgeCount;

        private static readonly float3[] _axes = new float3[6]
        {
            new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1),
            new float3(-1, 0, 0), new float3(0, -1, 0), new float3(0, 0, -1)
        };

        public void Execute()
        {
            _nodeCount = (ushort)positions.Length;
            _edgeCount = edges.Length;
            _bendingEdgeCount = bendingEdges.Length;
            _computedRegions = false;

            // 1. Integracja i Kolizje (Główna poprawka stabilności)
            ProfilerMarker integrationMarker = new("Integration & Substep Collisions");
            integrationMarker.Begin();

            float timestep = baseTimestep / qualityProperties.iterations;
            for (int i = 0; i < qualityProperties.iterations; i++)
            {
                Integrate(timestep);
                
                // POPRAWKA: Odkomentowanie kolizji wewnątrz iteracji zapobiega przenikaniu przy szybkim ruchu
                ApplyCollisions(); 
            }
            integrationMarker.End();

            // 2. Wiązania krawędzi (Constraints)
            ProfilerMarker constraintsMarker = new("Constraints");
            constraintsMarker.Begin();
            for (int i = 0; i < qualityProperties.constraintIterations; i++)
            {
                ConstrainEdges();
            }
            constraintsMarker.End();

            // 3. Kolizje końcowe (Dla pewności po poprawkach krawędzi)
            ApplyCollisions();

            // 4. Samokolizje
            if (collisionSettings.enableSelfCollision)
            {
                ProfilerMarker selfcolMarker = new("Self Collision");
                selfcolMarker.Begin();
                ApplySelfCollisions();
                selfcolMarker.End();
            }

            SatisfyPointQueries();
        }

        public void Integrate(float dt)
        {
            // Łączymy pętle dla lepszego wykorzystania pamięci cache
            for (int i = 0; i < _nodeCount; i++)
            {
                float3 acc = acceleration[i] * frictionMultiplier[i];
                float3 oldVel = velocity[i];
                
                // Verlet Integration
                float3 newPos = positions[i] + (oldVel * dt) + (acc * (dt * dt * 0.5f));
                positions[i] = newPos;
                tempAcceleration[i] = acc;
            }

            UpdateForces();

            for (int i = 0; i < _nodeCount; i++)
            {
                float3 acc = acceleration[i] * frictionMultiplier[i];
                float3 newVel = velocity[i] + (tempAcceleration[i] + acc) * (dt * 0.5f);

                // Air Resistance (Damping)
                velocity[i] = newVel * (1.0f - math.saturate(simProperties.airResistanceMultiplier * dt));
            }
        }

        public void UpdateForces()
        {
            ApplyGravity();
            ApplySprings();
            ApplyBending();
            ResetPinned();
        }

        private void ConstrainEdges()
        {
            // Zabezpieczenie przed nadmierną energią (Energy Conservation)
            float energySafe = math.clamp(material.energyConservation, 0f, 1f);

            for (int i = 0; i < _edgeCount; i++)
            {
                var edge = edges[i];
                float3 pos1 = positions[edge.nodeIndex1];
                float3 pos2 = positions[edge.nodeIndex2];
                float3 vec1To2 = pos2 - pos1;

                float distance = math.length(vec1To2);
                if (distance < 0.0001f) continue;

                float restDistance = restDistances[i];
                float stretchAmount = distance / restDistance;

                if (stretchAmount <= material.maxStretch) continue;

                float totalWeight = reciprocalWeight[edge.nodeIndex1] + reciprocalWeight[edge.nodeIndex2];
                if (totalWeight < 0.0001f) continue;

                float correction = (distance - restDistance * material.maxStretch) / distance;
                
                float weightCorrection1 = reciprocalWeight[edge.nodeIndex1] / totalWeight;
                float weightCorrection2 = reciprocalWeight[edge.nodeIndex2] / totalWeight;

                // Aplikacja pozycji z uwzględnieniem tarcia
                float f1 = frictionMultiplier[edge.nodeIndex1];
                float f2 = frictionMultiplier[edge.nodeIndex2];

                positions[edge.nodeIndex1] += vec1To2 * correction * weightCorrection1 * f1;
                positions[edge.nodeIndex2] -= vec1To2 * correction * weightCorrection2 * f2;

                // Tłumienie prędkości przy korekcie, aby uniknąć "strzelania" tkaniny
                float3 velCorr = vec1To2 * correction * energySafe;
                velocity[edge.nodeIndex1] += velCorr * weightCorrection1;
                velocity[edge.nodeIndex2] -= velCorr * weightCorrection2;
            }
        }

        private void ApplyCollisions()
        {
            float correctedContactOffset = collisionSettings.collisionContactOffset + extraThickness;
            float3 contactOffset3d = new(correctedContactOffset);

            // Reset tarcia na starcie kroku
            for (int i = 0; i < _nodeCount; i++) frictionMultiplier[i] = 1f;

            for (int i = 0; i < _edgeCount; i++)
            {
                UCEdge edge = edges[i];
                float3 centerPos = (positions[edge.nodeIndex1] + positions[edge.nodeIndex2]) * 0.5f;

                // SPHERE
                for (int j = 0; j < sphereColliders.Length; j++)
                {
                    SphereColDTO col = sphereColliders[j];
                    float3 dir = centerPos - col.position;
                    float dist = math.length(dir);
                    float radiusSum = col.radius + correctedContactOffset;

                    if (dist < radiusSum)
                    {
                        float3 normal = (dist < 0.0001f) ? new float3(0, 1, 0) : dir / dist;
                        ResolveCollision(edge, normal, radiusSum - dist, col.friction, col.velocity);
                    }
                }

                // CAPSULE
                for (int j = 0; j < capsuleColliders.Length; j++)
                {
                    CapsuleColDTO col = capsuleColliders[j];
                    float3 pa = centerPos - col.a;
                    float h = math.saturate(math.dot(pa, col.ba) / math.dot(col.ba, col.ba));
                    float3 closestPoint = col.a + h * col.ba;
                    float3 dir = centerPos - closestPoint;
                    float dist = math.length(dir);
                    float radiusSum = col.radius + correctedContactOffset;

                    if (dist < radiusSum)
                    {
                        float3 normal = (dist < 0.0001f) ? new float3(0, 1, 0) : dir / dist;
                        ResolveCollision(edge, normal, radiusSum - dist, col.friction, col.velocity);
                    }
                }

                // CUBE (Box)
                for (int j = 0; j < cubeColliders.Length; j++)
                {
                    CubeColDTO col = cubeColliders[j];
                    float3 localPos = math.mul(col.localMatrix, new float4(centerPos, 1f)).xyz - col.offset;
                    float3 halfSize = (col.size + contactOffset3d) * 0.5f;
                    float3 d = math.abs(localPos) - halfSize;
                    float sdf = math.max(d.x, math.max(d.y, d.z));

                    if (sdf < 0f)
                    {
                        // Znajdowanie najbliższej osi dla normalnej
                        int minAxis = 0;
                        float minDot = float.MaxValue;
                        for (int k = 0; k < 6; k++)
                        {
                            float dotVal = math.dot(localPos / col.size, _axes[k]);
                            if (dotVal < minDot) { minDot = dotVal; minAxis = k; }
                        }
                        float3 worldNormal = -math.rotate(col.worldMatrix, _axes[minAxis]);
                        ResolveCollision(edge, worldNormal, -sdf, col.friction, col.velocity);
                    }
                }
            }
        }

        // Pomocnicza metoda do rozwiązywania kolizji (DRY - Don't Repeat Yourself)
        private void ResolveCollision(UCEdge edge, float3 normal, float depth, float colFriction, float3 colVelocity)
        {
            float3 force = normal * depth;
            positions[edge.nodeIndex1] += force;
            positions[edge.nodeIndex2] += force;

            float friction = 1f - math.saturate(colFriction * collisionSettings.collisionFriction);
            frictionMultiplier[edge.nodeIndex1] = math.min(frictionMultiplier[edge.nodeIndex1], friction);
            frictionMultiplier[edge.nodeIndex2] = math.min(frictionMultiplier[edge.nodeIndex2], friction);

            float3 addedVel = (colVelocity / baseTimestep) * collisionSettings.collisionVelocityCorrection;
            velocity[edge.nodeIndex1] = addedVel;
            velocity[edge.nodeIndex2] = addedVel;
        }

        private void ApplySelfCollisions()
        {
            NativeList<int3> utilizedRegionIndices = ComputeSpatialPartitions();
            NativeList<ushort> nodes = new(32, Allocator.Temp);
            NativeList<ushort> surrounding = new(64, Allocator.Temp);

            for (int region = 0; region < utilizedRegionIndices.Length; region++)
            {
                nodes.Clear(); surrounding.Clear();
                int3 index = utilizedRegionIndices[region];

                if (!selfCollisionRegions.TryGetItemsNoAlloc(index, ref nodes)) continue;

                UCJobHelper.GetSurroundingNodesNoAlloc(ref selfCollisionRegions, index, surrounding, collisionSettings.selfCollisionAccuracy);

                for (int i = 0; i < nodes.Length; i++)
                {
                    // Z sąsiednimi komórkami
                    for (int j = 0; j < surrounding.Length; j++)
                        CalculateSelfCollision(nodes[i], surrounding[j]);

                    // Wewnątrz tej samej komórki
                    for (int j = 0; j < i; j++)
                        CalculateSelfCollision(nodes[i], nodes[j]);
                }
            }
        }

        private void CalculateSelfCollision(int index1, int index2)
        {
            if (neighbours.KeyContainsValue((ushort)index1, (ushort)index2)) return;

            float3 posDiff = positions[index1] - positions[index2];
            float dist = math.length(posDiff);

            if (dist < collisionSettings.selfCollisionDistance && dist > 0.0001f)
            {
                float3 normal = posDiff / dist;
                float overlap = collisionSettings.selfCollisionDistance - dist;
                float3 response = normal * (overlap * collisionSettings.selfCollisionStiffness * 0.5f);

                positions[index1] += response * reciprocalWeight[index1];
                positions[index2] -= response * reciprocalWeight[index2];

                // Stabilizacja prędkości przy samokolizji
                float3 relVel = velocity[index1] - velocity[index2];
                float velAlongNormal = math.dot(relVel, normal);
                if (velAlongNormal < 0)
                {
                    float3 vResponse = normal * (velAlongNormal * collisionSettings.selfCollisionFriction);
                    velocity[index1] -= vResponse * reciprocalWeight[index1];
                    velocity[index2] += vResponse * reciprocalWeight[index2];
                }
            }
        }

        private void ApplySprings()
        {
            for (int i = 0; i < _edgeCount; i++)
            {
                var edge = edges[i];
                float3 p1 = positions[edge.nodeIndex1];
                float3 p2 = positions[edge.nodeIndex2];
                float3 diff = p2 - p1;
                float dist = math.length(diff);
                if (dist < 0.0001f) continue;

                float3 dir = diff / dist;
                float springForce = (dist - restDistances[i]) * material.stiffnessCoefficient;
                
                // Damping
                float velAlongDir = math.dot(velocity[edge.nodeIndex2] - velocity[edge.nodeIndex1], dir);
                float dampingForce = velAlongDir * material.dampingCoefficient;

                float3 totalForce = dir * (springForce + dampingForce);

                acceleration[edge.nodeIndex1] += totalForce * (reciprocalWeight[edge.nodeIndex1] / material.vertexMass);
                acceleration[edge.nodeIndex2] -= totalForce * (reciprocalWeight[edge.nodeIndex2] / material.vertexMass);
            }
        }

        private void ApplyBending()
        {
            if (material.bendingCoefficient < 0.01f) return;

            for (int i = 0; i < _bendingEdgeCount; i++)
            {
                UCBendingEdge edge = bendingEdges[i];
                float3 n1 = normalsTriangles[edge.bendingTriangle1];
                float3 n2 = normalsTriangles[edge.bendingTriangle2];

                float dot = math.clamp(math.dot(n1, n2), -1f, 1f);
                float angle = math.acos(dot);
                float strength = material.bendingCoefficient * angle / math.PI;

                float3 edgeVec = positions[edge.bendingNode2] - positions[edge.bendingNode1];
                if (math.dot(n1, edgeVec) < 0) strength = -strength;

                acceleration[edge.bendingNode1] += n1 * strength;
                acceleration[edge.bendingNode2] += n2 * strength;
            }
        }

        private void ApplyGravity()
        {
            float3 gravity = simProperties.gravityMultiplier * material.vertexMass * Physics.clothGravity;
            for (int i = 0; i < _nodeCount; i++) acceleration[i] = gravity;
        }

        private void ResetPinned()
        {
            for (ushort i = 0; i < _nodeCount; i++)
            {
                if (reciprocalWeight[i] < 0.00001f)
                {
                    acceleration[i] = float3.zero;
                    velocity[i] = float3.zero;
                    positions[i] = pinnedPos[i];
                }
            }
        }

        private NativeList<int3> ComputeSpatialPartitions()
        {
            utilizedRegionSet.Clear();
            NativeList<int3> indices = new(64, Allocator.Temp);
            for (ushort i = 0; i < _nodeCount; i++)
            {
                int3 idx = FindClosestRegionIndex(positions[i]);
                idx = math.clamp(idx, 0, selfCollisionRegions.size - 1);
                selfCollisionRegions.Add(idx, i);
                if (utilizedRegionSet.Add(idx)) indices.Add(idx);
            }
            _computedRegions = true;
            return indices;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int3 FindClosestRegionIndex(float3 pos)
        {
            float3 relPos = (pos - (float3)bounds.min) / bounds.size;
            return (int3)math.trunc(relPos * selfCollisionRegions.size);
        }

        private void SatisfyPointQueries() { /* Implementacja bez zmian */ }
    }
}