using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

public class Shooting : MonoBehaviour
{
    [SerializeField] private Vector3 shift;
    [SerializeField] private float range;
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
        if (Physics.Raycast(transform.position + shift,transform.forward, out RaycastHit raycastHit, range))
        {
            if (!raycastHit.transform.tag.Equals("Softwall")) return;
            TransformTo2D(raycastHit.transform.gameObject, raycastHit.transform.InverseTransformPoint(raycastHit.point));
        }
    }

    private void TransformTo2D(GameObject pGameObject, Vector3 pHitPosition)
    {
        MeshFilter meshFilter = pGameObject.GetComponent<MeshFilter>();
        List<Vector3> originalVertices = new List<Vector3>();
        meshFilter.mesh.GetVertices(originalVertices);

        //Mesh projection
        List<Vector2> vertices2D = new List<Vector2>();
        Matrix4x4 projectionMatrix = new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0),
            new Vector4(0, 0, 0, 0), new Vector4(0, 0, 0, 0));
        
        foreach (var currentVertex in originalVertices)
        {
            Vector4 vertexAs4D = new Vector4(currentVertex.x, currentVertex.y, currentVertex.z, 0);
            Vector4 flatVertex4D = projectionMatrix * vertexAs4D;
            Vector2 flatVertex2D = new Vector3(flatVertex4D.x, flatVertex4D.y);
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
        int step = 4;
        for (int i = 0; i < step; i++)
        {
            Vector2 newVertex = hitPosition + new Vector2(Mathf.Cos((2 * Mathf.PI / step) * i), Mathf.Sin((2 * Mathf.PI / step) * i)) * 0.1f;
            hole.Add(newVertex);
            Debug.Log($"Hole element {i} was {newVertex}");
        }

        polygon.AddHole(hole);

        //Weiler-Atherton Clipping
        //Todo: Implement Weiler-Atherton algorithm
        
        // //Ear-clipping triangulation
         _earClipping.SetupClipping(polygon);
        
        //=========================================================Clean========================================================
        Mesh newMesh = new Mesh();
        Vector3[] flatVertex3D = new Vector3[polygon.vertices2D.Count];
        int itr = 0;
        foreach(var vertex in polygon.vertices2D)
        {
            Vector2 currentVertex = vertex;
            flatVertex3D[itr++] = new Vector3(currentVertex.x, currentVertex.y, 0);
        }
        
        newMesh.vertices = flatVertex3D;
        newMesh.triangles = _earClipping.Triangulate();
        
        newMesh.RecalculateBounds();
        newMesh.RecalculateTangents();
        newMesh.RecalculateNormals();
        meshFilter.mesh = newMesh;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(transform.position + shift, transform.forward * range);
    }
}
