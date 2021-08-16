using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;

public class EarClipping
{
    public LinkedList<Vertex> vertices;

    public void SetupClipping(Polygon pPolygon)
    {
        vertices = new LinkedList<Vertex>();
        foreach(var currentVertex in pPolygon.outerPolygon)
        {
            vertices.AddLast(currentVertex);
        }

        bool ranOnce = true;
        LinkedListNode<Vertex> startNode = vertices.Find(pPolygon.mutuallyVisible.Value);
        for (LinkedListNode<Vertex> currentNode = pPolygon.innerXMost; currentNode != pPolygon.innerXMost || ranOnce; currentNode = currentNode.Next ?? currentNode.List.First)
        {
            ranOnce = false;
            LinkedListNode<Vertex> test = new LinkedListNode<Vertex>(currentNode.Value);
            vertices.AddAfter(startNode, test);
            startNode = test;
        }

        vertices.AddAfter(startNode, pPolygon.innerXMostCopy);
        startNode = vertices.Find(pPolygon.innerXMostCopy);
        vertices.AddAfter(startNode, pPolygon.mutuallyVisibleCopy);

        //Then check if convex :)
        for (LinkedListNode<Vertex> current = vertices.First; current != null; current = current.Next)
        {
            var previous = current.Previous ?? current.List.Last;
            var next = current.Next ?? current.List.First;
            current.Value.isConvex = IsConvex(previous.Value.position, current.Value.position, next.Value.position);
        }
    }

    public int[] Triangulate()
    {
        int[] tris = new int[(vertices.Count - 2) * 3];
        int trisIndex = 0;

        while (vertices.Count >= 3)
        {
            for (LinkedListNode<Vertex> currentVertexNode = vertices.First;
                currentVertexNode != null;
                currentVertexNode = currentVertexNode.Next)
            {
                var previous = currentVertexNode.Previous ?? currentVertexNode.List.Last;
                var next = currentVertexNode.Next ?? currentVertexNode.List.First;
                if (currentVertexNode.Value.isConvex)
                {
                    if (IsEar(previous.Value.position, currentVertexNode.Value.position, next.Value.position))
                    {
                        vertices.Remove(currentVertexNode);
                        
                        //Create triangle
                        tris[trisIndex * 3] = previous.Value.index;
                        tris[trisIndex * 3 + 1] = currentVertexNode.Value.index;
                        tris[trisIndex * 3 + 2] = next.Value.index;
                        trisIndex += 1;

                        Debug.Log($"The vertex {currentVertexNode.Value.index} at position {currentVertexNode.Value.position} was convex and had an ear");

                        //Check if previous and next vertices are reflex and if so; recalculate
                        if (!previous.Value.isConvex)
                        {
                            bool convex = IsConvex((previous.Previous ?? previous.List.Last).Value.position, previous.Value.position, next.Value.position);
                            Debug.Log($"The previous vertex IsConvex is {convex}");
                            previous.Value.isConvex = convex;
                        }

                        if (!next.Value.isConvex)
                        {
                            bool convex = IsConvex(previous.Value.position, next.Value.position, (next.Next ?? next.List.First).Value.position);
                            Debug.Log("The previous vertex IsConvex is " + convex);
                            next.Value.isConvex = convex;
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
        return (abcArea - (pbcArea + pacArea + pabArea)) > 0.01f;
    }

    private float TriangleArea(Vector2 a, Vector2 b, Vector2 c)
    {
        return Math.Abs((a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y)) / 2.0f);
    }

    private bool IsEar(Vector2 previous, Vector2 current, Vector2 next)
    {
        //Is this possibly an ear?
        foreach (var vertex in vertices)
        {
            //if (previous == vertex.position || current == vertex.position || next == vertex.position) continue;
            
            if (IsInTriangle(previous, current, next, vertex.position))
                return false;
        }
         
        return true;
    }

    private bool IsConvex(Vector2 previous, Vector2 current, Vector2 next)
    {
        //Convex or Reflex?
        Vector2 edge1 = (next - current).normalized;
        Vector2 edge2 = (previous - current).normalized;
        float cross = edge1.x * edge2.y - edge1.y * edge2.x;
        float dot = Vector2.Dot(edge1, edge2);
        float angle = Mathf.Atan2(Mathf.Abs(cross), dot) * Mathf.Rad2Deg;
        if (cross > 0) angle = 360 - angle;
        //Debug.Log("Angle was " + angle);
        return angle < 180.0f;
    }
}

public class Vertex
{
    public readonly Vector2 position;
    public readonly int index;
    public bool isConvex;

    public Vertex(Vector2 pPosition, int pIndex)
    {
        position = pPosition;
        index = pIndex;
    }
}


public class Polygon
{
    public readonly LinkedList<Vertex> outerPolygon;
    public LinkedList<Vertex> innerPolygon;
    public Vertex innerXMostCopy, mutuallyVisibleCopy;
    public LinkedListNode<Vertex> innerXMost, mutuallyVisible;
    private List<Vector2> _vertices;
    private int itr = 0;

    public Polygon(List<Vector2> pOuterPolygon)
    {
        outerPolygon = new LinkedList<Vertex>();
        _vertices = new List<Vector2>();
        foreach (var currentVertex in pOuterPolygon)
        {
            outerPolygon.AddLast(new Vertex(currentVertex, itr++));
            _vertices.Add(currentVertex);
        }
    }

    public void AddHole(List<Vector2> pHoleVertices)
    {
        innerPolygon = new LinkedList<Vertex>();
        foreach (var currentVertex in pHoleVertices)
        {
            innerPolygon.AddLast(new Vertex(currentVertex, itr++));
            _vertices.Add(currentVertex);
        }
        
        //This one must be duplicated
        innerXMost = FindXMostVertex();
        
        //This one must be duplicated
        mutuallyVisible = FindMutuallyVisibleVertex();

        mutuallyVisibleCopy = new Vertex(mutuallyVisible.Value.position, itr++);
        _vertices.Add(mutuallyVisibleCopy.position);
        innerXMostCopy = new Vertex(innerXMost.Value.position, itr++);
        _vertices.Add(innerXMostCopy.position);
    }

    private LinkedListNode<Vertex> FindXMostVertex()
    {
        LinkedListNode<Vertex> currentBest = null;
        for(LinkedListNode<Vertex> currentVertex = innerPolygon.First; currentVertex != null; currentVertex = currentVertex.Next)
        {
            if (currentBest == null || currentVertex.Value.position.x > currentBest.Value.position.x)
                currentBest = currentVertex;
        }
        
        return currentBest;
    }

    private LinkedListNode<Vertex> FindMutuallyVisibleVertex()
    {
        LinkedListNode<Vertex> currentVertex = outerPolygon.First;
        if (currentVertex == null)
        {
            Debug.LogError("Cannot find polygon vertices! Abort!");
            return null;
        }
        
        LinkedListNode<Vertex> closestIntersectionEdge = null;
        float closestIntersectionDistance = float.PositiveInfinity;
        Vector2 directionVector = Vector2.right;

        //Source: https://rootllama.wordpress.com/2014/06/20/ray-line-segment-intersection-test-in-2d/
        for (int i = 0; i < outerPolygon.Count; i++)
        {
            //Circular extension
            var next = currentVertex.Next ?? currentVertex.List.First;
            
            //Edge of the outer polygon must be right the x-most vertex of the inner polygon.
            if (currentVertex.Value.position.x <= innerXMost.Value.position.x || next.Value.position.x <= innerXMost.Value.position.x)
            {
                // Debug.Log($"Inner polygon vertex with the highest x-coordinate value was {innerXMost.Value.position}, " +
                //           $"while the current edge was at {currentVertex.Value.position} and {next.Value.position}");
                currentVertex = currentVertex.Next;
                continue;
            }
            
            //Debug.Log($"Found edge right to vertex with the highest x-coordinate value at {currentVertex.Value.position} and {next.Value.position}");

            //=========================================================Clean========================================================
            
            //Todo:
            //Current and next should be the way around, but gives crazy values
            
            Vector2 v1 = innerXMost.Value.position - next.Value.position;
            Vector2 v2 = currentVertex.Value.position - next.Value.position;
            Vector2 v3 = new Vector2(-directionVector.y, directionVector.x);

            float t1 = Vector3.Cross(v2, v1).magnitude / Vector2.Dot(v2, v3);
            float t2 = Vector2.Dot(v1, v3) / Vector2.Dot(v2, v3);

            if (t1 >= 0 && t2 >= 0 && t2 <= 1)
            {
                Vector2 intersectionPoint = innerXMost.Value.position + directionVector * t1;
                //Debug.Log("Intersection point was at " + intersectionPoint);
                Vector2 intersectionDistance = intersectionPoint - innerXMost.Value.position;
                
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
        LinkedListNode<Vertex> mutuallyVisibleVertex = closestIntersectionEdge.Value.position.x > secondPointOfEdge.Value.position.x
            ? closestIntersectionEdge
            : secondPointOfEdge;
        
        
        //Debug.Log($"The mutually visible point was vertex with index {mutuallyVisibleVertex.Value.index} and position {mutuallyVisibleVertex.Value.position}.");
        return mutuallyVisibleVertex;
    }

    public List<Vector2> GetVertices()
    {
        return _vertices;
    }
}
