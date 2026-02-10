using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UCloth
{
    /// <summary>
    /// Creates simulation data from a mesh.
    /// </summary>
    public class UCMeshPreprocessor : IUCPreprocessor // : IUCPreprocessor (Zakładam, że interfejs istnieje w twoim projekcie)
    {
        private List<UCEdge> _edges;
        private Dictionary<UCEdge, int> _edgeRefCount;

        private Dictionary<float3, ushort> _hashedVertices;
        private NativeParallelMultiHashMap<ushort, ushort> _neighbours;

        /// <param name="input"> Mesh to convert to sim data. </param>
        public bool ConvertMeshData(object input, out UCMeshData data)
        {
            Mesh mesh = input as Mesh;

            if (mesh == null)
            {
                Debug.LogError("Input is not a Mesh!");
                data = new UCMeshData();
                return false;
            }

            // --- AUTO-FIX READ/WRITE (Tylko w Editorze) ---
            if (!mesh.isReadable)
            {
#if UNITY_EDITOR
                if (!MakeMeshReadable(mesh))
                {
                    Debug.LogError($"Mesh '{mesh.name}' is not readable and could not be fixed automatically. Please turn on 'Read/Write' in model import settings manually.");
                    data = new UCMeshData();
                    return false;
                }
                else
                {
                    Debug.Log($"<color=green>Fixed:</color> Automatically enabled 'Read/Write' for mesh '{mesh.name}'.");
                }
#else
                Debug.LogError($"Mesh '{mesh.name}' is not readable. You MUST turn on 'Read/Write' in model import settings for build.");
                data = new UCMeshData();
                return false;
#endif
            }
            // ---------------------------------------------

            if (mesh.vertexCount >= ushort.MaxValue)
            {
                Debug.LogWarning($"Mesh has more than {ushort.MaxValue} vertices. You might experience issues simulating it. " +
                    $"Consider decreasing vertex count.");
            }

            //--- COMBINE VERTICES

            _hashedVertices = new Dictionary<float3, ushort>(mesh.vertexCount);

            NativeArray<int> renderToSimLookup = new NativeArray<int>(mesh.vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            List<float3> positions = new List<float3>(mesh.vertexCount);

            ushort uniqueVertId = 0;
            Vector3[] meshVertices = mesh.vertices; // Cache vertices access

            for (int i = 0; i < mesh.vertexCount; i++)
            {
                float3 position = meshVertices[i];

                if (_hashedVertices.ContainsKey(position))
                {
                    int trueVertex = _hashedVertices[position];
                    renderToSimLookup[i] = trueVertex;
                }
                else
                {
                    _hashedVertices[position] = uniqueVertId;
                    renderToSimLookup[i] = uniqueVertId;
                    uniqueVertId++;

                    positions.Add(position);
                }
            }

            //--- CONNECTED EDGES

            _edges = new List<UCEdge>(positions.Count * 3);
            _neighbours = new NativeParallelMultiHashMap<ushort, ushort>(positions.Count, Allocator.Persistent);
            _edgeRefCount = new Dictionary<UCEdge, int>();

            Dictionary<UCTriangle, int> trianglesCache = new Dictionary<UCTriangle, int>();
            int[] meshTriangles = mesh.triangles; // Cache triangles access
            int trisCount = meshTriangles.Length / 3;

            for (int i = 0; i < trisCount; i++)
            {
                int triangleIndex = i * 3;

                int ind1 = meshTriangles[triangleIndex];
                int ind2 = meshTriangles[triangleIndex + 1];
                int ind3 = meshTriangles[triangleIndex + 2];

                UCEdge edge1 = GetHashedEdge(meshVertices, ind1, ind2);
                UCEdge edge2 = GetHashedEdge(meshVertices, ind2, ind3);
                UCEdge edge3 = GetHashedEdge(meshVertices, ind1, ind3);

                var triKey = new UCTriangle(edge1.nodeIndex1, edge1.nodeIndex2, edge2.nodeIndex2);
                if (!trianglesCache.ContainsKey(triKey))
                {
                    trianglesCache.Add(triKey, i);
                }

                CreateEdge(edge1);
                CreateEdge(edge2);
                CreateEdge(edge3);
            }

            //--- BOUNDING EDGES

            List<UCEdge> boundingEdges = new List<UCEdge>();
            for (int i = 0; i < trisCount; i++)
            {
                int triangleIndex = i * 3;

                int ind1 = meshTriangles[triangleIndex];
                int ind2 = meshTriangles[triangleIndex + 1];
                int ind3 = meshTriangles[triangleIndex + 2];

                UCEdge edge1 = GetHashedEdge(meshVertices, ind1, ind2);
                UCEdge edge2 = GetHashedEdge(meshVertices, ind2, ind3);
                UCEdge edge3 = GetHashedEdge(meshVertices, ind1, ind3);

                if (_edgeRefCount[edge1] == 1) boundingEdges.Add(new UCEdge((ushort)ind1, (ushort)ind2));
                if (_edgeRefCount[edge2] == 1) boundingEdges.Add(new UCEdge((ushort)ind2, (ushort)ind3));
                if (_edgeRefCount[edge3] == 1) boundingEdges.Add(new UCEdge((ushort)ind3, (ushort)ind1));
            }

            //--- BENDING ELEMENTS

            List<UCBendingEdge> bendingEdges = new List<UCBendingEdge>();
            for (int i = 0; i < _edges.Count; i++)
            {
                UCEdge edge = _edges[i];

                ushort index1 = edge.nodeIndex1;
                ushort index2 = edge.nodeIndex2;
                
                // Używamy GetValuesForKey, aby pobrać iteratory
                var enu1 = _neighbours.GetValuesForKey(index1);
                var enu2 = _neighbours.GetValuesForKey(index2);

                List<int> commonNeighbours = new List<int>();

                // Iteracja po sąsiadach
                foreach (var n1 in enu1)
                {
                    foreach (var n2 in enu2)
                    {
                         if (n1 == n2)
                         {
                             if (!commonNeighbours.Contains(n2))
                                 commonNeighbours.Add(n2);
                         }
                    }
                }

                if (commonNeighbours.Count == 2)
                {
                    UCTriangle triKey1 = new UCTriangle(index1, index2, (ushort)commonNeighbours[0]);
                    UCTriangle triKey2 = new UCTriangle(index2, index1, (ushort)commonNeighbours[1]);

                    if (trianglesCache.ContainsKey(triKey1) && trianglesCache.ContainsKey(triKey2))
                    {
                        int tri1 = trianglesCache[triKey1];
                        int tri2 = trianglesCache[triKey2];
                        UCBendingEdge bendingEdge = new UCBendingEdge(commonNeighbours[0], commonNeighbours[1], tri1, tri2);
                        bendingEdges.Add(bendingEdge);
                    }
                }
            }

            // Sort edges by Y
            _edges = _edges.OrderBy(e => (positions[e.nodeIndex1] + positions[e.nodeIndex2]).y).ToList();

            // Check for stray vertices
            HashSet<ushort> usedVertices = new HashSet<ushort>(positions.Count);
            for (int i = 0; i < _edges.Count; i++)
            {
                usedVertices.Add(_edges[i].nodeIndex1);
                usedVertices.Add(_edges[i].nodeIndex2);
            }

            if (positions.Count - usedVertices.Count > 0)
                Debug.LogWarning("Some vertices are not connected by any edges. They will not be properly simulated.");

            data = new UCMeshData
            {
                positions = positions,
                edges = _edges,
                bendingEdges = bendingEdges,
                boundingEdges = boundingEdges,
                triangles = new NativeArray<int>(meshTriangles, Allocator.Persistent),
                neighbours = _neighbours,
                renderToSimLookup = renderToSimLookup
            };
            return true;
        }

        //--- HELPER METHODS

        private void CreateEdge(UCEdge edge)
        {
            if (!DoesEdgeAlreadyExist(edge))
            {
                _edges.Add(edge);
                _neighbours.Add(edge.nodeIndex1, edge.nodeIndex2);
                _neighbours.Add(edge.nodeIndex2, edge.nodeIndex1);
            }

            if (!_edgeRefCount.ContainsKey(edge))
                _edgeRefCount.Add(edge, 1);
            else
                _edgeRefCount[edge]++;
        }

        private UCEdge GetHashedEdge(Vector3[] vertices, int index1, int index2)
        {
            float3 pos1 = vertices[index1];
            float3 pos2 = vertices[index2];
            ushort ind1Hashed = FindHashedIndex(pos1);
            ushort ind2Hashed = FindHashedIndex(pos2);
            return new UCEdge(ind1Hashed, ind2Hashed);
        }

        private ushort FindHashedIndex(float3 pos)
        {
            if (!_hashedVertices.ContainsKey(pos))
                throw new Exception($"Could not find hashed vertex position {pos} in UC preprocessor!");
            return _hashedVertices[pos];
        }

        private bool DoesEdgeAlreadyExist(UCEdge edge)
        {
            if (!_neighbours.ContainsKey(edge.nodeIndex1)) return false;
            if (!_neighbours.ContainsKey(edge.nodeIndex2)) return false;

            // Poprawione sprawdzanie zamiast nieistniejącego KeyContainsValue
            bool foundForward = false;
            foreach (var val in _neighbours.GetValuesForKey(edge.nodeIndex1))
            {
                if (val == edge.nodeIndex2) { foundForward = true; break; }
            }
            if (foundForward) return true;

            bool foundBackward = false;
            foreach (var val in _neighbours.GetValuesForKey(edge.nodeIndex2))
            {
                if (val == edge.nodeIndex1) { foundBackward = true; break; }
            }
            return foundBackward;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Tries to set the mesh to readable in the Import Settings.
        /// </summary>
        private bool MakeMeshReadable(Mesh mesh)
        {
            if (!mesh) return false;
            string assetPath = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(assetPath)) return false; // Mesh might be procedural/runtime generated

            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                return true;
            }
            return false;
        }
#endif
    }
}