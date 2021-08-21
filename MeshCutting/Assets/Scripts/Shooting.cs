using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal.Internal;
using Random = UnityEngine.Random;

public class Shooting : MonoBehaviour
{
    [SerializeField] private Vector3 shift;
    [SerializeField] private float range;

    [Header("Destruction")]
    [SerializeField] private int cutPolygonStep;

    private PlayerInput _playerInput;
    private EarClipping _earClipping;
    private Dictionary<GameObject, Polygon> _allPolygons;
    private Material lastHitMaterial;
    private AudioSource sound;
    
    private void Start()
    {
        _playerInput = transform.parent.parent.GetComponent<PlayerInput>();
        _playerInput.actions.FindAction("Shoot").performed += Shoot;
        _earClipping = new EarClipping();
        _allPolygons = new Dictionary<GameObject, Polygon>();
        sound = GetComponent<AudioSource>();
    }

    private void Shoot(InputAction.CallbackContext pCallback)
    {
        sound.Play();
        if (Physics.Raycast(transform.position + shift, transform.forward, out RaycastHit raycastHit, range))
        {
            if (!raycastHit.transform.tag.Equals("Softwall")) return;
            CutPolygon(raycastHit.transform.gameObject, raycastHit.transform.InverseTransformPoint(raycastHit.point));
        }
    }

    private void CutPolygon(GameObject pGameObject, Vector3 pHitPosition)
    {
        //Projection
        MeshFilter meshFilter = pGameObject.GetComponent<MeshFilter>();
        lastHitMaterial = pGameObject.GetComponent<MeshRenderer>().material;
        
        Polygon currentPolygon;

        if (_allPolygons.Keys.Contains(pGameObject)) currentPolygon = _allPolygons[pGameObject];
        else
        {
            List<Vector2> flatVertices2D = TransformTo2D(meshFilter.mesh);
            //Sorting
            //TODO: Implement a convex hull algorithm
            //Only do this at a primitive box the first time to order its vertices, this is due to the order after the projection.
            //This will be replaced by a convex hull algorithm.
            //Vertices are now rotated clockwise!
            Vector2 vecSwap = flatVertices2D.ElementAt(2);
            flatVertices2D[2] = flatVertices2D[3];
            flatVertices2D[3] = vecSwap;
            
            //Create polygon
            currentPolygon = new Polygon(flatVertices2D);
            _allPolygons.Add(pGameObject, currentPolygon);
        }
        
        //Create cut-polygon
        Polygon cutPolygon = CreateCutPolygon(pHitPosition);
        
        //Weiler-Atherton Clipping
        //Todo: Implement Weiler-Atherton algorithm
        
        //Ear clipping triangulation
        currentPolygon.AddInnerPolygon(cutPolygon);
        _earClipping.SetupClipping(currentPolygon);
        
        Mesh newMesh = new Mesh();
        Vector3[] flatVertices3D = new Vector3[_earClipping.straightList.Count];

        for (int i = 0; i < _earClipping.straightList.Count; i++)
        {
            Vector2 currentVertex = _earClipping.straightList.ElementAt(i);
            flatVertices3D[i] = new Vector3(currentVertex.x, currentVertex.y,0);
            
        }

        newMesh.vertices = flatVertices3D;
        newMesh.triangles = _earClipping.Triangulate();

        Vector2[] newUVs = new Vector2[_earClipping.straightList.Count];
        for (int i = 0; i < _earClipping.straightList.Count; i++)
        {
            newUVs[i] = new Vector2(0.5f, 0.5f) - _earClipping.straightList.ElementAt(i);
        }

        newMesh.uv = newUVs;
        newMesh.RecalculateNormals();
        meshFilter.mesh = newMesh;
        
        CreateCutPolygonGameObject(cutPolygon.polygon.ToList(), pGameObject);
    }
    
    private List<Vector2> TransformTo2D(Mesh pMesh)
    {
        //This simply returns the meshes vertices as 2D vectors
        List<Vector2> vertices2D = new List<Vector2>();
        foreach (var currentVertex in pMesh.vertices)
        {
            if (!vertices2D.Contains(currentVertex))
            {
                vertices2D.Add(currentVertex);
            }
        }

        return vertices2D;
    }
    
    private Polygon CreateCutPolygon(Vector2 pHitPosition)
    {
        List<Vector2> cutVertices = new List<Vector2>();
        for (int i = 0; i < cutPolygonStep; i++) 
        {
            Vector2 newVertex = pHitPosition + new Vector2(Mathf.Cos((2 * Mathf.PI / cutPolygonStep) * i), 
                Mathf.Sin((2 * Mathf.PI / cutPolygonStep) * i)) * Random.Range(0.08f, 0.14f);
            cutVertices.Add(newVertex);
        }
        
        return new Polygon(cutVertices);
    }

    private GameObject CreateCutPolygonGameObject(List<Vector2> pVertices, GameObject pGameObject)
    {
        pVertices.Reverse();
        Polygon cutPolygon = new Polygon(pVertices);
        _earClipping.SetupClipping(cutPolygon);
        
        Vector3[] flatCutVertex3D = new Vector3[pVertices.Count * 2];
        Vector2[] cutPolygonVertices = cutPolygon.polygon.ToArray();
        for (var i = 0; i < cutPolygon.polygon.Count * 2; i++)
        {
            Vector2 currentVertex = cutPolygonVertices[i % cutPolygonVertices.Length];
            flatCutVertex3D[i] = new Vector3(currentVertex.x, currentVertex.y, 0);
        }
        
        int[] triangles = _earClipping.Triangulate();
        int[] newTriangles = new int[triangles.Length * 2];
        
        for (int i = 0; i < triangles.Length / 3; i++)
        {
            newTriangles[i * 3] = triangles[i * 3];
            newTriangles[i * 3 + 1] = triangles[i * 3 + 1];
            newTriangles[i * 3 + 2] = triangles[i * 3 + 2];
        }

        for (int i = 0; i < triangles.Length / 3; i++)
        {
            int tris1 = pVertices.Count + triangles[i * 3];
            int tris2 = pVertices.Count +triangles[i * 3 + 1];

            int temp = tris1;
            tris1 = tris2;
            tris2 = temp;

            newTriangles[triangles.Length + i * 3] = tris1;
            newTriangles[triangles.Length + i * 3 + 1] = tris2;
            newTriangles[triangles.Length + i * 3 + 2] = pVertices.Count + triangles[i * 3 + 2];
        }
                
        Mesh cutMesh = new Mesh();
        cutMesh.vertices = flatCutVertex3D;
        cutMesh.triangles = newTriangles;
        Vector2[] newUVs = new Vector2[flatCutVertex3D.Length];
        for (int i = 0; i < flatCutVertex3D.Length; i++)
        {
            newUVs[i] = new Vector2(0.5f, 0.5f) - (Vector2)flatCutVertex3D.ElementAt(i);
        }

        cutMesh.uv = newUVs;
        cutMesh.RecalculateNormals();
        
        GameObject cutPolygonGameObject = new GameObject();
        cutPolygonGameObject.AddComponent<MeshFilter>().mesh = cutMesh;
        cutPolygonGameObject.AddComponent<MeshRenderer>().material = lastHitMaterial;
        BoxCollider cutPolygonCollider = cutPolygonGameObject.AddComponent<BoxCollider>();
        Vector3 colliderSize =  cutPolygonCollider.size;
        cutPolygonCollider.size = new Vector3(colliderSize.x, colliderSize.y, 0.1f);

        Rigidbody cutPolygonRigidbody = cutPolygonGameObject.AddComponent<Rigidbody>();
        cutPolygonRigidbody.AddForce(transform.forward * 5.0f, ForceMode.Impulse);
        cutPolygonRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        cutPolygonGameObject.transform.position = pGameObject.transform.position;
        cutPolygonGameObject.transform.localScale = pGameObject.transform.localScale;
        cutPolygonGameObject.transform.rotation = pGameObject.transform.rotation;

        return cutPolygonGameObject;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(transform.position + shift, transform.forward * range);
    }
}