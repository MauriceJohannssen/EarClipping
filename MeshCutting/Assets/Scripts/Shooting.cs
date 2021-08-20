using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

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

        Vector2[] test = new Vector2[_earClipping.straightList.Count];
        for (int i = 0; i < _earClipping.straightList.Count; i++)
        {
            test[i] = new Vector2(0.5f, 0.5f) - _earClipping.straightList.ElementAt(i);
        }

        newMesh.uv = test;
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
        
        Vector3[] flatCutVertex3D = new Vector3[pVertices.Count];
        int itr = 0;
        
        foreach(var vertex in cutPolygon.polygon.ToList())
        {
            flatCutVertex3D[itr++] = new Vector3(vertex.x, vertex.y, 0);
        }
                
        Mesh cutMesh = new Mesh();
        cutMesh.vertices = flatCutVertex3D;
        cutMesh.triangles = _earClipping.Triangulate();
        Vector2[] test = new Vector2[cutPolygon.polygon.Count];
        for (int i = 0; i < cutPolygon.polygon.Count; i++)
        {
            test[i] = new Vector2(0.5f, 0.5f) - cutPolygon.polygon.ElementAt(i);
        }

        cutMesh.uv = test;
        cutMesh.RecalculateNormals();
        
        GameObject cutPolygonGameObject = new GameObject();
        cutPolygonGameObject.AddComponent<MeshFilter>().mesh = cutMesh;
        cutPolygonGameObject.AddComponent<MeshRenderer>().material = lastHitMaterial;
        cutPolygonGameObject.AddComponent<MeshCollider>().convex = true;
        
        cutPolygonGameObject.AddComponent<Rigidbody>().AddForce(transform.forward * 500.0f, ForceMode.Force);
        
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