using System;
using System.Collections.Generic;
using System.Security;
using UnityEngine;

public class EarClipping
{
    private LinkedList<Vertex> _vertices;

    public void SetupClipping(List<Vector2> pVertices)
    {
        LinkedList<Vector2> vertices = new LinkedList<Vector2>(pVertices);
        _vertices = new LinkedList<Vertex>();
        LinkedListNode<Vertex> currentVertex = null;
        int itr = 0;
        for (LinkedListNode<Vector2> current = vertices.First; current != null; current = current.Next)
        {
            var previous = current.Previous ?? current.List.Last;
            var next = current.Next ?? current.List.First;
            Vertex vertex = new Vertex(current.Value, itr++, IsConvex(previous.Value, current.Value, next.Value));
            if (currentVertex == null)
            {
                currentVertex = _vertices.AddFirst(vertex);
            }
            else
            {
                _vertices.AddAfter(currentVertex, vertex);
            }
        }

        foreach (var ejs in this._vertices)
        {
            Debug.Log($"Vertex {ejs.index} had the position: {ejs.position} and IsConvex is: {ejs.isConvex}");
        }
    }

    public int[] Triangulate()
    {
        int[] tris = new int[(_vertices.Count - 2) * 3];
        int trisIndex = 0;
        while (_vertices.Count >= 3)
        {
            LinkedListNode<Vertex> currentVertexNode = _vertices.First;
            for (int i = 0; i < _vertices.Count; i++)
            {
                var previous = currentVertexNode.Previous ?? currentVertexNode.List.Last;
                var next = currentVertexNode.Next ?? currentVertexNode.List.First;
                if (currentVertexNode.Value.isConvex)
                {
                    //Possible ear
                    if (IsEar(previous.Value.position, currentVertexNode.Value.position, next.Value.position))
                    {
                        //Ear found
                        Debug.Log("Found ear");

                        //1. Remove ear tip
                        _vertices.Remove(currentVertexNode);
                        Debug.Log($"First index {previous.Value.index}, second is {currentVertexNode.Value.index} and third is {next.Value.index}");
                        tris[trisIndex * 3] = previous.Value.index;
                        tris[trisIndex * 3 + 1] = currentVertexNode.Value.index;
                        tris[trisIndex * 3 + 2] = next.Value.index;
                        trisIndex++;
                        break;
                    }
                }
            }
        }

        return tris;
    }

    private bool IsInTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 point)
    {
        float abcArea = TriangleArea(a, b, c);
        float pbcArea = TriangleArea(point, b, c);
        float pacArea = TriangleArea(a, point, c);
        float pabArea = TriangleArea(a, b, point);
        return abcArea == (pbcArea + pacArea + pabArea);
    }

    private float TriangleArea(Vector2 a, Vector2 b, Vector2 c)
    {
        return Math.Abs((a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y)) / 2.0f);
    }

    private bool IsEar(Vector2 previous, Vector2 current, Vector2 next)
    {
        //Is this possibly an ear?
        foreach (var vertex in _vertices)
        {
            if (vertex.position == previous || vertex.position == current || vertex.position == next)
                continue;
    
            if (IsInTriangle(previous, current, next, vertex.position))
                return false;
        }
        
        return true;
    }

    private bool IsConvex(Vector2 previous, Vector2 current, Vector2 next)
    {
        //Convex or Reflex?
        float angle = Vector2.Angle(next - current, previous - current);
        if(angle == 180.0f) Debug.LogError("Invalid angle in polygon");
        return angle < 180;
    }
}

public class Vertex
{
    public readonly Vector2 position;
    public int index;
    public bool isConvex;

    public Vertex(Vector2 pPosition, int pIndex, bool pIsConvex)
    {
        position = pPosition;
        index = pIndex;
        isConvex = pIsConvex;
    }
}
