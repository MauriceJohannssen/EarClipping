using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

public class Shooting : MonoBehaviour
{
    [SerializeField] private Vector3 shift;
    [SerializeField] private float range;
    [SerializeField] private Material material;
    private PlayerInput _playerInput;
    private EarClipping _earClipping;

    // Start is called before the first frame update
    private void Start()
    {
        _playerInput = transform.parent.parent.GetComponent<PlayerInput>();
        _playerInput.actions.FindAction("Shoot").performed += Shoot;
        _earClipping = new EarClipping();
    }

    private void Shoot(InputAction.CallbackContext pCallback)
    {
        if (Physics.Raycast(transform.position + shift, transform.forward, out RaycastHit raycastHit, range))
        {
            if (!raycastHit.transform.tag.Equals("Softwall")) return;
            TransformTo2D(raycastHit.transform.gameObject,
                raycastHit.transform.InverseTransformPoint(raycastHit.point));
        }
    }

    private void TransformTo2D(GameObject pGameObject, Vector3 pHitPosition)
    {
        MeshFilter meshFilter = pGameObject.GetComponent<MeshFilter>();
        List<Vector3> originalVertices = new List<Vector3>();
        meshFilter.mesh.GetVertices(originalVertices);

        //Mesh projection
        List<Vector2> vertices2D = new List<Vector2>();
        foreach (var currentVertex in originalVertices)
        {
            Vector2 flatVertex2D = new Vector3(currentVertex.x, currentVertex.y);
            if (!vertices2D.Contains(flatVertex2D))
            {
                vertices2D.Add(flatVertex2D);
            }
        }
        
        //Todo: Implement any convex hull algorithm
        //Only do this at an OG box.
        //This will be replaced by a convex hull algorithm.
        //Vertices are now rotated clockwise!
        Vector2 vecSwap = vertices2D.ElementAt(2);
        vertices2D[2] = vertices2D[3];
        vertices2D[3] = vecSwap;
        
        //Polygon
        Polygon polygon = new Polygon(vertices2D);
        
        //Create hole polygon
        Vector2 hitPosition = new Vector2(pHitPosition.x, pHitPosition.y);
        List<Vector2> hole = new List<Vector2>();
        int step = 10;
        for (int i = 0; i < step; i++) 
        {
             Vector2 newVertex = hitPosition + new Vector2(Mathf.Cos((2 * Mathf.PI / step) * i), Mathf.Sin((2 * Mathf.PI / step) * i)) * Random.Range(0.05f, 0.15f);
             hole.Add(newVertex);
             //Debug.Log($"Hole element {i} was {newVertex}");
        }
        
        polygon.AddHole(hole);
        
        //Weiler-Atherton Clipping
        //Todo: Implement Weiler-Atherton algorithm
        
        //Ear-clipping triangulation
        _earClipping.SetupClipping(polygon);
        Mesh newMesh = new Mesh();
        Vector3[] flatVertex3D = new Vector3[_earClipping.vertices.Count];
        int itr = 0;
        foreach(var vertex in polygon.GetVertices())
        {
            flatVertex3D[itr++] = new Vector3(vertex.x, vertex.y, 0);
        }
        
        newMesh.vertices = flatVertex3D;
        newMesh.triangles = _earClipping.Triangulate();;
        
        newMesh.RecalculateNormals();
        meshFilter.mesh = newMesh;
        
        //Create cut polygon
        hole.Reverse();
        Polygon cutPolygon = new Polygon(hole);
        _earClipping.SetupClipping(cutPolygon);
        
        Mesh cutMesh = new Mesh();
        
        Vector3[] flatCutVertex3D = new Vector3[hole.Count];
        itr = 0;
        
        foreach(var vertex in cutPolygon.GetVertices())
        {
            flatCutVertex3D[itr++] = new Vector3(vertex.x, vertex.y, 0);
        }
        
        cutMesh.vertices = flatCutVertex3D;
        cutMesh.triangles = _earClipping.Triangulate();
        cutMesh.RecalculateNormals();
        
        GameObject cutPolygonGameObject = new GameObject();
        cutPolygonGameObject.AddComponent<MeshFilter>().mesh = cutMesh;
        cutPolygonGameObject.AddComponent<MeshRenderer>().material = material;
        cutPolygonGameObject.AddComponent<MeshCollider>().convex = true;
        cutPolygonGameObject.AddComponent<Rigidbody>().AddForce(transform.forward * 500.0f, ForceMode.Force);
        
        cutPolygonGameObject.transform.position = pGameObject.transform.position;
        cutPolygonGameObject.transform.localScale = pGameObject.transform.localScale;
        cutPolygonGameObject.transform.rotation = pGameObject.transform.rotation;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(transform.position + shift, transform.forward * range);
    }
}