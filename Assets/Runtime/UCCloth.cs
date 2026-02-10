using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace UCloth
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class UCCloth : MonoBehaviour
    {
        public UCPreprocessorType preprocessorType;

        [Tooltip("These colliders will be used at startup to determine if a vertex is pinned or not.")]
        public List<Collider> pinColliders = new List<Collider>();

        [Space]
        [Header("Settings")]
        public UCMaterial materialProperties;
        public UCSimulationProperties simulationProperties;
        public UCQualityProperties qualityProperties;
        public UCCollisionSettings collisionProperties;

        [Space]
        public SphereCollider[] sphereColliders;
        public CapsuleCollider[] capsuleColliders;
        public BoxCollider[] cubeColliders;

        [Header("Post processing")]
        public float thickness;
        public bool offsetFront;
        [Range(0f, 0.95f)]
        public float smoothing;

        public UCInternalSimData simData;

        private NativeArray<SphereColDTO> sphereColDTOs;
        private NativeArray<CapsuleColDTO> capsuleColDTOs;
        private NativeArray<CubeColDTO> cubeColDTOs;

        public List<IUCPostprocessor> postprocessors;
        internal IUCPostprocessor[] internalPostprocessors;

        private UCRenderer _ucRenderer;
        private UCAutoOptimizer _ucOptimizer;
        private UCColliderTracker _ucColliderTracker;
        private UCPinner _ucPinner;

        private MeshRenderer _meshRenderer;
        internal UCMeshData initialMeshData;
        internal NativeReference<UCAutoOptimizeData> optimizationData;

        private NativeList<UCPointQueryData> pointQueries;
        private NativeList<ushort> pointQueryResults;
        private NativeList<ushort> pointQueryIndexCounts;

        private JobHandle? _job;
        private JobHandle? _normalRecompute;
        private TaskCompletionSource<bool> onBeforeStart;
        private TaskCompletionSource<bool> onFinished;

        public event EventHandler OnSimulationFinished;

        private int _lastSimFrequency;
        private bool _firstFrame = true;
        private float _timestep;

        private void Start()
        {
            _ucPinner = new UCPinner(this);

            bool success = SetUpData(out var meshData);

            if (!success)
            {
                Debug.LogError("UCCloth: Setup failed. Check if 'Read/Write Enabled' is on in Mesh Import Settings.", this);
                return;
            }

            postprocessors = new List<IUCPostprocessor>();
            internalPostprocessors = new IUCPostprocessor[2];

            _ucRenderer = new UCRenderer(this, meshData, GetComponent<MeshFilter>(), GetComponent<MeshCollider>());
            _ucOptimizer = new UCAutoOptimizer(this);
            _ucColliderTracker = new UCColliderTracker();

            _meshRenderer = GetComponent<MeshRenderer>();
            InitializeTimer();
        }

        private void Reset()
        {
            UCInitializer.Initialize(this);
        }

        private void OnDestroy()
        {
            _job?.Complete();
            _normalRecompute?.Complete();
            
            _ucPinner?.Dispose();
            simData?.Dispose();

            if (sphereColDTOs.IsCreated) sphereColDTOs.Dispose();
            if (capsuleColDTOs.IsCreated) capsuleColDTOs.Dispose();
            if (cubeColDTOs.IsCreated) cubeColDTOs.Dispose();

            StopTimer();
            _ucRenderer?.Dispose();

            if (initialMeshData.triangles.IsCreated) initialMeshData.triangles.Dispose();
            if (initialMeshData.renderToSimLookup.IsCreated) initialMeshData.renderToSimLookup.Dispose();
            if (optimizationData.IsCreated) optimizationData.Dispose();

            if (pointQueries.IsCreated) pointQueries.Dispose();
            if (pointQueryResults.IsCreated) pointQueryResults.Dispose();
            if (pointQueryIndexCounts.IsCreated) pointQueryIndexCounts.Dispose();

            if (postprocessors != null) foreach (var p in postprocessors) p?.Dispose();
            if (internalPostprocessors != null) foreach (var p in internalPostprocessors) p?.Dispose();
        }

        private void Update()
        {
            if (simData == null || _ucPinner == null) return;

            if (!qualityProperties.minimizeLatency && _ucRenderer != null)
                _ucRenderer.ScheduleTransformations();

            _ucPinner.UpdateMoved();
            UpdateInternalPostprocessor();
            UpdateTimer();
        }

        private void LateUpdate()
        {
            if (_ucRenderer == null || simData == null) return;

            if (qualityProperties.minimizeLatency)
                _ucRenderer.ScheduleTransformations();

            _ucRenderer.UpdateRenderedMesh();
        }

        public void AttachPin(Collider col)
        {
            if (col == null || _ucPinner == null) return;
            if (!pinColliders.Contains(col))
            {
                pinColliders.Add(col);
                _ucPinner.SetUpDataPinned();
            }
        }

        public void DetachPin(Collider col)
        {
            if (col == null || _ucPinner == null) return;
            if (pinColliders.Contains(col))
            {
                pinColliders.Remove(col);
                _ucPinner.SetUpDataPinned();
            }
        }

        internal void FetchAndSimulate()
        {
            if (simData == null) return;
            ScheduleFinish();
            ScheduleStart();
        }

        private void ScheduleStart()
        {
            UpdateColliderDTOs();
            VerifyDataValidity();

            _timestep = Time.timeScale * qualityProperties.timeScaleMultiplier / qualityProperties.simFrequency;
            _timestep = math.clamp(_timestep, 0f, qualityProperties.maxTimestep);

            onBeforeStart?.SetResult(true);
            onBeforeStart = null;

            simData.CopyWriteableData();

            UCJob job = new UCJob()
            {
                positions = simData.cPositions,
                velocity = simData.cVelocity,
                acceleration = simData.cAcceleration,
                tempAcceleration = simData.cTempAcceleration,
                frictionMultiplier = simData.cFriction,
                selfCollisionRegions = simData.cSelfCollisionRegions,
                utilizedRegionSet = simData.cUtilizedSelfColRegions,
                edges = simData.edgesReadOnly,
                bendingEdges = simData.bendingEdgesReadOnly,
                neighbours = simData.neighboursReadOnly,
                normals = simData.normalsReadOnly,
                normalsTriangles = simData.triangleNormalsReadOnly,
                restDistances = simData.restDistancesReadOnly,
                reciprocalWeight = simData.cReciprocalWeight,
                pinnedPos = simData.cPinnedPositions,
                material = materialProperties,
                collisionSettings = collisionProperties,
                sphereColliders = sphereColDTOs,
                capsuleColliders = capsuleColDTOs,
                cubeColliders = cubeColDTOs,
                simProperties = simulationProperties,
                baseTimestep = _timestep,
                extraThickness = thickness / 1000f,
                qualityProperties = qualityProperties,
                pointQueries = pointQueries,
                pointQueryResults = pointQueryResults,
                pointQueryIndexCounts = pointQueryIndexCounts,
                bounds = _meshRenderer.bounds,
                optimizationData = optimizationData
            };

            _job = job.Schedule();
            _normalRecompute = GetNormalRecomputeJob().Schedule(_job.Value);
        }

        private void ScheduleFinish()
        {
            if (_job == null) return;

            _job.Value.Complete();
            onFinished?.SetResult(true);
            onFinished = null;
            OnSimulationFinished?.Invoke(this, null);

            if (_normalRecompute.HasValue) _normalRecompute.Value.Complete();

            if (_ucRenderer != null)
            {
                _ucRenderer.UpdateRenderingPositions(simData.cPositions);
                _ucRenderer.UpdateRenderingNormals(simData.normalsReadOnly);
                if (_firstFrame)
                {
                    _ucRenderer.ScheduleTransformations();
                    _firstFrame = false;
                }
            }

            UpdateAutooptimisation();

            if (sphereColDTOs.IsCreated) sphereColDTOs.Dispose();
            if (capsuleColDTOs.IsCreated) capsuleColDTOs.Dispose();
            if (cubeColDTOs.IsCreated) cubeColDTOs.Dispose();

            _job = null;
        }

        private UCNormalComputeJob GetNormalRecomputeJob()
        {
            return new UCNormalComputeJob()
            {
                vertices = simData.positionsReadOnly,
                normals = simData.cNormals,
                triangleNormals = simData.cTriangleNormals,
                triangles = initialMeshData.triangles,
                renderToSimLookup = initialMeshData.renderToSimLookup
            };
        }

        private void InitializeTimer()
        {
            InvokeRepeating(nameof(FetchAndSimulate), 0f, 1f / qualityProperties.simFrequency);
        }

        private void UpdateTimer()
        {
            if (_lastSimFrequency != qualityProperties.simFrequency)
            {
                StopTimer();
                InitializeTimer();
            }
            _lastSimFrequency = qualityProperties.simFrequency;
        }

        private void StopTimer()
        {
            CancelInvoke(nameof(FetchAndSimulate));
        }

        private void UpdateColliderDTOs()
        {
            var clothBounds = _meshRenderer.bounds;
            Vector3 expansionOffset = (clothBounds.size * 0.01f) + (Vector3.one * collisionProperties.collisionContactOffset);

            var validSpheres = UCColliderDTOHelper.FilterColliders(sphereColliders, clothBounds, expansionOffset);
            sphereColDTOs = new NativeArray<SphereColDTO>(validSpheres.Count, Allocator.TempJob);
            for (int i = 0; i < validSpheres.Count; i++)
            {
                var s = validSpheres[i];
                float radiusScale = math.max(math.max(s.transform.localScale.x, s.transform.localScale.y), s.transform.localScale.z);
                sphereColDTOs[i] = new SphereColDTO { position = s.transform.TransformPoint(s.center), radius = s.radius * radiusScale, velocity = _ucColliderTracker.GetAndUpdateVelocity(s), friction = UCColliderDTOHelper.GetFriction(s) };
            }

            var validCaps = UCColliderDTOHelper.FilterColliders(capsuleColliders, clothBounds, expansionOffset);
            capsuleColDTOs = new NativeArray<CapsuleColDTO>(validCaps.Count, Allocator.TempJob);
            for (int i = 0; i < validCaps.Count; i++)
            {
                var c = validCaps[i];
                Vector3 qH = c.height * c.transform.localScale.y * 0.25f * c.transform.up;
                capsuleColDTOs[i] = new CapsuleColDTO { a = c.transform.TransformPoint(c.center) + qH, ba = -2 * qH, radius = c.radius * math.max(c.transform.localScale.x, c.transform.localScale.z), velocity = _ucColliderTracker.GetAndUpdateVelocity(c), friction = UCColliderDTOHelper.GetFriction(c) };
            }

            var validCubes = UCColliderDTOHelper.FilterColliders(cubeColliders, clothBounds, expansionOffset);
            cubeColDTOs = new NativeArray<CubeColDTO>(validCubes.Count, Allocator.TempJob);
            for (int i = 0; i < validCubes.Count; i++)
            {
                var c = validCubes[i];
                cubeColDTOs[i] = new CubeColDTO { position = c.transform.position, offset = c.center, size = c.size, localMatrix = c.transform.worldToLocalMatrix, worldMatrix = c.transform.localToWorldMatrix, velocity = _ucColliderTracker.GetAndUpdateVelocity(c), friction = UCColliderDTOHelper.GetFriction(c) };
            }

            if (simData.cSelfCollisionRegions.IsCreated()) simData.cSelfCollisionRegions.Dispose();
            int3 regions = (int3)math.ceil(_meshRenderer.bounds.size * collisionProperties.gridDensity);
            simData.cSelfCollisionRegions = new Native3DHashmapArray<ushort>(regions, Allocator.TempJob);
        }

        private void UpdateAutooptimisation()
        {
            if (collisionProperties.AutoAdjustGridDensity != UCAutoAdjustGridOption.None && optimizationData.IsCreated)
                collisionProperties.gridDensity = _ucOptimizer.OptimizeDensity(optimizationData.Value);
        }

        private void UpdateInternalPostprocessor()
        {
            if (internalPostprocessors == null) return;
            if (internalPostprocessors[0] == null && smoothing >= 0.0000001f) internalPostprocessors[0] = new UCSmoothingPostprocessor(this);
            else if (internalPostprocessors[0] != null && smoothing < 0.0000001f) internalPostprocessors[0].ScheduledToCleanup = true;

            if (internalPostprocessors[1] == null && math.abs(thickness) >= 0.0000001f) internalPostprocessors[1] = new UCThicknessPostprocessor(this);
            else if (internalPostprocessors[1] != null && math.abs(thickness) < 0.0000001f) internalPostprocessors[1].ScheduledToCleanup = true;
        }

        private void VerifyDataValidity()
        {
            materialProperties.vertexMass = math.max(0.01f, materialProperties.vertexMass);
            qualityProperties.iterations = math.max(1, qualityProperties.iterations);
            collisionProperties.gridDensity = math.max(0, collisionProperties.gridDensity);
        }

        private bool SetUpData(out UCMeshData data)
        {
            data = default;
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;

            IUCPreprocessor preprocessor = new UCMeshPreprocessor();
            if (!preprocessor.ConvertMeshData(mf.sharedMesh, out data)) return false;

            initialMeshData = data;
            float3[] positions = new float3[data.positions.Count];
            for (int i = 0; i < data.positions.Count; i++) positions[i] = transform.localToWorldMatrix.MultiplyPoint(data.positions[i]);

            simData = new UCInternalSimData
            {
                cPositions = new NativeArray<float3>(positions, Allocator.Persistent),
                edgesReadOnly = new NativeArray<UCEdge>(data.edges.ToArray(), Allocator.Persistent),
                bendingEdgesReadOnly = new NativeArray<UCBendingEdge>(data.bendingEdges.ToArray(), Allocator.Persistent),
                neighboursReadOnly = data.neighbours,
                cNormals = new NativeArray<float3>(positions.Length, Allocator.Persistent),
                cTriangleNormals = new NativeArray<float3>(initialMeshData.triangles.Length, Allocator.Persistent),
                restDistancesReadOnly = new NativeArray<float>(data.edges.Count, Allocator.Persistent)
            };

            for (int i = 0; i < data.edges.Count; i++) simData.restDistancesReadOnly[i] = math.distance(positions[data.edges[i].nodeIndex1], positions[data.edges[i].nodeIndex2]);

            simData.cVelocity = new NativeArray<float3>(positions.Length, Allocator.Persistent);
            simData.cAcceleration = new NativeArray<float3>(positions.Length, Allocator.Persistent);
            simData.cTempAcceleration = new NativeArray<float3>(positions.Length, Allocator.Persistent);
            simData.cFriction = new NativeArray<float>(positions.Length, Allocator.Persistent);
            simData.cUtilizedSelfColRegions = new NativeParallelHashSet<int3>(100, Allocator.Persistent);
            optimizationData = new NativeReference<UCAutoOptimizeData>(Allocator.Persistent);

            pointQueries = new NativeList<UCPointQueryData>(Allocator.Persistent);
            pointQueryResults = new NativeList<ushort>(Allocator.Persistent);
            pointQueryIndexCounts = new NativeList<ushort>(Allocator.Persistent);

            _ucPinner.SetUpDataPinned();
            simData.PrepareCopies();
            simData.CopyWriteableData();
            GetNormalRecomputeJob().Run();

            return true;
        }
    }
}