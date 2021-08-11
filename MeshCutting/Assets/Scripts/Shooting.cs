using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class Shooting : MonoBehaviour
{
    [SerializeField] private Vector3 shift;
    [SerializeField] private float range;
    private PlayerInput _playerInput;

    // Start is called before the first frame update
    private void Start()
    {
        _playerInput = transform.parent.parent.GetComponent<PlayerInput>();
        _playerInput.actions.FindAction("Shoot").performed += Shoot;
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
        List<Vector3> flatVertices = new List<Vector3>();
        
        //Mesh projection
        Matrix4x4 projectionMatrix = new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0),
            new Vector4(0, 0, 0, 0), new Vector4(0, 0, 0, 0));
        int itr = 0;
        foreach (var vertex in originalVertices)
        {
            Debug.Log($"Vertex {vertex} was at iterator position {itr}");
            itr++;
            Vector4 vertexAs4D = new Vector4(vertex.x, vertex.y, vertex.z, 0);
            Vector4 flatVertex4D = projectionMatrix * vertexAs4D;
            Vector3 flatVertex3D = new Vector3(flatVertex4D.x, flatVertex4D.y, flatVertex4D.z);
            if (!flatVertices.Contains(vertex))
                flatVertices.Add(vertex);
        }
        Debug.Log("flat vertices has vertices " + flatVertices.Count);
        
        //Polygon Insertion
        //Weiler-Atherton Clipping

        Debug.Log("Individual vertices: " + flatVertices.Count);

        //Ear-clipping triangulation
        
        
        
        Mesh lellek = new Mesh();
        lellek.vertices = flatVertices.ToArray();
        lellek.triangles = new[] {0, 1, 2, 1, 3,2};
        Vector3 normall = -pGameObject.transform.forward;
        lellek.normals = new[]
        {
            normall,
            normall,           
            normall,
            normall
        };

        meshFilter.mesh = lellek;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(transform.position + shift, transform.forward * range);
    }
}
