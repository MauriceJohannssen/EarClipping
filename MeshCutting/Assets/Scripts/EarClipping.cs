using System;
using System.Collections.Generic;
using UnityEngine;

public class EarClipping
{
    private LinkedList<Vertex> _vertices;

    public void SetupClipping(Polygon pPolygon)
    {
        _vertices = new LinkedList<Vertex>();
        LinkedList<Vector2> vertices = new LinkedList<Vector2>(pPolygon.vertices2D);
        LinkedListNode<Vertex> currentVertex = null;
        int itr = 0;
        for (LinkedListNode<Vector2> current = vertices.First; current != null; current = current.Next)
        {
            var previous = current.Previous ?? current.List.Last;
            var next = current.Next ?? current.List.First;
            Vertex vertex = new Vertex(current.Value, itr++, IsConvex(previous.Value, current.Value, next.Value));
            if (currentVertex == null) currentVertex = _vertices.AddFirst(vertex);
            else _vertices.AddAfter(currentVertex, vertex);
        }

        foreach (var cee in _vertices)
        {
            Debug.Log($"Vertices loaded in are index: {cee.index} with the position {cee.position} and IsConvex is: {cee.isConvex}");
        }
    }

    public int[] Triangulate()
    {
        int[] tris = new int[(_vertices.Count - 2) * 3];
        int trisIndex = 0;
        while (_vertices.Count >= 3)
        {
            LinkedListNode<Vertex> currentVertexNode = _vertices.First;
            foreach(var vertex in _vertices)
            {
                var previous = currentVertexNode.Previous ?? currentVertexNode.List.Last;
                var next = currentVertexNode.Next ?? currentVertexNode.List.First;
                if (currentVertexNode.Value.isConvex)
                {
                    //Possible ear?
                    if (IsEar(previous.Value.position, currentVertexNode.Value.position, next.Value.position))
                    {
                        //Remove ear tip.
                        //Can remove this here even though I am using foreach since I am breaking anyway.
                        _vertices.Remove(vertex);
                        Debug.Log($"First index {previous.Value.index}, second is {currentVertexNode.Value.index} and third is {next.Value.index}");
                        //Create triangle
                        tris[trisIndex * 3] = previous.Value.index;
                        tris[trisIndex * 3 + 1] = currentVertexNode.Value.index;
                        tris[trisIndex * 3 + 2] = next.Value.index;
                        trisIndex++;
                        
                        //Check if previous and next vertices are reflex and if so; recalculate
                        if (!previous.Value.isConvex)
                        {
                            previous.Value.isConvex = IsConvex(previous.Value.position, next.Value.position, (next.Next ?? _vertices.First).Value.position);
                        }

                        if (!next.Value.isConvex)
                        {
                            next.Value.isConvex = IsConvex((previous.Previous ?? _vertices.Last).Value.position, previous.Value.position, next.Value.position);
                        }
                        
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
    public readonly int index;
    public bool isConvex;

    public Vertex(Vector2 pPosition, int pIndex, bool pIsConvex)
    {
        position = pPosition;
        index = pIndex;
        isConvex = pIsConvex;
    }
}


public class Polygon
{
    public readonly LinkedList<Vector2> vertices2D;

    public Polygon(List<Vector2> pVertices2D)
    {
        vertices2D = new LinkedList<Vector2>(pVertices2D);
    }

    public void AddHole(List<Vector2> pVertices)
    {
        var xMostVertex = FindXMostVertex(pVertices);
        var mutuallyVisibleVertex = FindMutuallyVisibleVertex(xMostVertex);
        //=========================================================Clean========================================================

        LinkedList<Vector2> verticesAsList = new LinkedList<Vector2>(pVertices);
        LinkedListNode<Vector2> startNode = verticesAsList.Find(xMostVertex);
        LinkedListNode<Vector2> lastAddedNode = null;
        bool iteratedOnce = true;

        for (LinkedListNode<Vector2> current = verticesAsList.Find(xMostVertex); current != startNode || iteratedOnce; current = current.Next ?? current.List.First)
        {
            iteratedOnce = false;
            LinkedListNode<Vector2> test = new LinkedListNode<Vector2>(current.Value);
            vertices2D.AddAfter(mutuallyVisibleVertex, test);
            mutuallyVisibleVertex = test;
            lastAddedNode = test;
        }

        vertices2D.AddAfter(lastAddedNode, xMostVertex);
        vertices2D.AddAfter(lastAddedNode.Next, mutuallyVisibleVertex.Value);
    }

    private Vector2 FindXMostVertex(List<Vector2> pVertices)
    {
        Vector2 currentBest = Vector2.negativeInfinity;
        foreach (var vertex in pVertices)
        {
            if (vertex.x > currentBest.x)
                currentBest = vertex;
        }

        Debug.Log("X most position of the hole was " + currentBest);
        return currentBest;
    }

    private LinkedListNode<Vector2> FindMutuallyVisibleVertex(Vector2 pXMostVertex)
    {
        LinkedListNode<Vector2> currentVertex = vertices2D.First;
        if (currentVertex == null)
        {
            Debug.LogError("Cannot find polygon vertices! Abort!");
            return null;
        }
        
        LinkedListNode<Vector2> closestIntersectionEdge = null;
        float closestIntersectionDistance = float.PositiveInfinity;
        Vector2 directionVector = Vector2.right;

        //Source: https://rootllama.wordpress.com/2014/06/20/ray-line-segment-intersection-test-in-2d/
        for (int i = 0; i < vertices2D.Count; i++)
        {
            //Circular extension
            var next = currentVertex.Next ?? currentVertex.List.First;
            
            //Edge of the outer polygon must be right the x-most vertex of the inner polygon.
            if (currentVertex.Value.x <= pXMostVertex.x || next.Value.x <= pXMostVertex.x)
            {
                Debug.Log("x-most vertex is " + pXMostVertex + " while the current edge was at " + currentVertex.Value + " and " + next.Value);
                currentVertex = currentVertex.Next;
                continue;
            }
            
            Debug.Log("Found edge right to x-most point which is " + currentVertex.Value + "and " + next.Value);

            //=========================================================Clean========================================================
            
            //Todo:
            //Current and next should be the way around, but gives crazy values
            
            Vector2 v1 = pXMostVertex - next.Value;
            Vector2 v2 = currentVertex.Value - next.Value;
            Vector2 v3 = new Vector2(-directionVector.y, directionVector.x);

            float t1 = Vector3.Cross(v2, v1).magnitude / Vector2.Dot(v2, v3);
            float t2 = Vector2.Dot(v1, v3) / Vector2.Dot(v2, v3);

            if (t1 >= 0 && t2 >= 0 && t2 <= 1)
            {
                Vector2 intersectionPoint = pXMostVertex + directionVector * t1;
                Debug.Log("Intersection point was at " + intersectionPoint);
                Vector2 intersectionDistance = intersectionPoint - pXMostVertex;
                
                if (closestIntersectionEdge == null || intersectionDistance.magnitude < closestIntersectionDistance)
                {
                    closestIntersectionEdge = currentVertex;
                    closestIntersectionDistance = intersectionDistance.magnitude;
                }
            }
            
            currentVertex = currentVertex.Next;
        }

        if (closestIntersectionEdge == null)
        {
            Debug.LogError("No edge could be found!");
            return null;
        }
        
        //Find best-edge x-most point
        var secondPointOfEdge = closestIntersectionEdge.Next ?? closestIntersectionEdge.List.First;
        LinkedListNode<Vector2> mutuallyVisibleVertex = closestIntersectionEdge.Value.x > secondPointOfEdge.Value.x
            ? closestIntersectionEdge
            : secondPointOfEdge;
        
        
        Debug.Log("The mutually visible point was " + mutuallyVisibleVertex.Value);
        return secondPointOfEdge;
    }
}
