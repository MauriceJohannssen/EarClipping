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
            Debug.Log($"You hit: {raycastHit.transform.gameObject.name} !");
            if (!raycastHit.transform.tag.Equals("Softwall")) return;
            TransformTo2D(raycastHit.transform.gameObject);
        }
    }

    private void TransformTo2D(GameObject pGameObject)
    {
        MeshFilter meshFilter = pGameObject.GetComponent<MeshFilter>();
        Debug.Log("Total vertices was " + meshFilter.mesh.vertexCount);
        List<Vector3> originalVertices = new List<Vector3>();
        meshFilter.mesh.GetVertices(originalVertices);
        List<Vector2> flatVertices = new List<Vector2>();
        
        //Mesh projection
        Matrix4x4 projectionMatrix = new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0),
            new Vector4(0, 0, 0, 0), new Vector4(0, 0, 0, 0));
        int itr = 0;
        foreach (var vertex in originalVertices)
        {
            //Debug.Log($"Vertex {vertex} was at iterator position {itr}");
            itr++;
            Vector4 vertexAs4D = new Vector4(vertex.x, vertex.y, vertex.z, 0);
            Vector4 flatVertex4D = projectionMatrix * vertexAs4D;
            Vector3 flatVertex2D = new Vector3(flatVertex4D.x, flatVertex4D.y, flatVertex4D.z);
            if (!flatVertices.Contains(flatVertex2D))
                flatVertices.Add(flatVertex2D);
        }

        //Only do this at an OG box!!!
        Vector2 vecSwap = flatVertices.ElementAt(2);
        flatVertices[2] = flatVertices[3];
        flatVertices[3] = vecSwap;

        //Debug.Log("flat vertices has vertices " + flatVertices.Count);
        
        //Polygon Insertion
        //Weiler-Atherton Clipping

        //Debug.Log("Individual vertices: " + flatVertices.Count);

        //Ear-clipping triangulation
        _earClipping.SetupClipping(flatVertices);

        Mesh newMesh = new Mesh();
        Vector3[] flatVertex3D = new Vector3[flatVertices.Count];
        for (int i = 0; i < flatVertices.Count; i++)
        {
            Vector2 currentVertex = flatVertices[i];
            flatVertex3D[i] = new Vector3(currentVertex.x, currentVertex.y, 0);
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
