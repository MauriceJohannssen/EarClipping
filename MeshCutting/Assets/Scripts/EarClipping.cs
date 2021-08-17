using System;
using System.Collections.Generic;
using System.Security;
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

        //Only sort the vertices when there's an inner polygon
        if (pPolygon.innerPolygon != null)
        {
            bool notOnceRan = true;
            LinkedListNode<Vertex> startNode = vertices.Find(pPolygon.mutuallyVisible.Value);
            for (LinkedListNode<Vertex> currentNode = pPolygon.innerXMost; currentNode != pPolygon.innerXMost || notOnceRan; 
                currentNode = currentNode.Next ?? currentNode.List.First)
            {
                notOnceRan = false;
                LinkedListNode<Vertex> newNode = new LinkedListNode<Vertex>(currentNode.Value);
                vertices.AddAfter(startNode, newNode);
                startNode = newNode;
            }

            vertices.AddAfter(startNode, pPolygon.innerXMostCopy);
            startNode = vertices.Find(pPolygon.innerXMostCopy);
            vertices.AddAfter(startNode, pPolygon.mutuallyVisibleCopy);
        }

        //Then check if vertices are convex.
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
                        
                        //Check if previous and next vertices are reflex and if so; recalculate
                        if (!previous.Value.isConvex)
                        {
                            bool convex = IsConvex((previous.Previous ?? previous.List.Last).Value.position, previous.Value.position, next.Value.position);
                            previous.Value.isConvex = convex;
                        }

                        if (!next.Value.isConvex)
                        {
                            bool convex = IsConvex(previous.Value.position, next.Value.position, (next.Next ?? next.List.First).Value.position);
                            next.Value.isConvex = convex;
                        }

                        break;
                    }
                }
            }
        }

        return tris;
    }
    
    private bool IsEar(Vector2 previous, Vector2 current, Vector2 next)
    {
        //This checks for a possible ear.
        foreach (var vertex in vertices)
        {
            if (previous == vertex.position || current == vertex.position || next == vertex.position) continue;
            if (Triangle.IsInTriangle(previous, current, next, vertex.position))
                return false;
        }
         
        return true;
    }

    private bool IsConvex(Vector2 previous, Vector2 current, Vector2 next)
    {
        //Convex or Reflex?
        Vector2 edge1 = (next - current).normalized;
        Vector2 edge2 = (previous - current).normalized;
        return Vector2Extension.AngleFullDegrees(edge1, edge2) < 180.0f;
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
    public LinkedListNode<Vertex> innerXMost, mutuallyVisible;
    public Vertex innerXMostCopy, mutuallyVisibleCopy;
    private readonly List<Vector2> _vertices;
    private int index;

    public Polygon(List<Vector2> pOuterPolygon)
    {
        outerPolygon = new LinkedList<Vertex>();
        _vertices = new List<Vector2>();
        foreach (var currentVertex in pOuterPolygon)
        {
            outerPolygon.AddLast(new Vertex(currentVertex, index++));
            _vertices.Add(currentVertex);
        }
    }

    public void AddInnerPolygon(List<Vector2> pInnerPolygonVertices)
    {
        innerPolygon = new LinkedList<Vertex>();
        foreach (var currentVertex in pInnerPolygonVertices)
        {
            innerPolygon.AddLast(new Vertex(currentVertex, index++));
            _vertices.Add(currentVertex);
        }
        
        //These are just nodes to the x-most vertex of the inner polygon, and the mutually visible of the outer polygon.
        //The sole purpose is to save the position in the LinkedList after which I have to insert the cut polygon and the two copies of
        //these vertices in order to cut the polygon open.
        innerXMost = FindXMostVertex();
        mutuallyVisible = FindMutuallyVisibleVertex();

        mutuallyVisibleCopy = new Vertex(mutuallyVisible.Value.position, index++);
        _vertices.Add(mutuallyVisibleCopy.position);
        innerXMostCopy = new Vertex(innerXMost.Value.position, index++);
        _vertices.Add(innerXMostCopy.position);
    }

    private LinkedListNode<Vertex> FindXMostVertex()
    {
        //This searches for the vertex with the highest values on the x-axis.
        LinkedListNode<Vertex> currentBest = null;
        for(LinkedListNode<Vertex> currentVertex = innerPolygon.First; currentVertex != null; currentVertex = currentVertex.Next)
        {
            if (currentBest == null || currentVertex.Value.position.x > currentBest.Value.position.x)
            {
                currentBest = currentVertex;
            }
        }
        
        return currentBest;
    }

    private LinkedListNode<Vertex> FindMutuallyVisibleVertex()
    {
        Vector2? intersectionPoint = null;
        LinkedListNode<Vertex> closestIntersectionEdge = FindClosestIntersectionEdge(ref intersectionPoint);
        
        //Find best-edge x-most point
        var secondPointOfEdge = closestIntersectionEdge.Next ?? closestIntersectionEdge.List.First;
        LinkedListNode<Vertex> mutuallyVisibleVertex = closestIntersectionEdge.Value.position.x > secondPointOfEdge.Value.position.x
            ? closestIntersectionEdge
            : secondPointOfEdge;
        
        //This verifies whether there are vertices in the triangle created by the inner x-most point, the intersection point with the outer polygon
        //and the mutually visible vertex. Since these vertices can potentially occlude the mutually visible vertex.
        float currentShortestAngle = float.PositiveInfinity;
        for (LinkedListNode<Vertex> outerVertex = outerPolygon.First; outerVertex != null; outerVertex = outerVertex.Next)
        {
            if (intersectionPoint == null)
            {
                throw new ArgumentException("No intersection-point found.");
            }
            
            if(Triangle.IsInTriangle(innerXMost.Value.position, (Vector2)intersectionPoint ,mutuallyVisibleVertex.Value.position, outerVertex.Value.position))
            {
                //Take the one with the smallest angle
                float currentAngle = Vector2Extension.AngleFullDegrees((Vector2) intersectionPoint - innerXMost.Value.position,
                    mutuallyVisibleVertex.Value.position - innerXMost.Value.position);

                if (currentAngle < currentShortestAngle)
                {
                    mutuallyVisibleVertex = outerVertex;
                    currentShortestAngle = currentAngle;
                }
            }
        }
        
        return mutuallyVisibleVertex;
    }

    private LinkedListNode<Vertex> FindClosestIntersectionEdge(ref Vector2? pIntersectionPoint)
    {
        LinkedListNode<Vertex> currentVertex = outerPolygon.First;
        
        if (currentVertex == null)
        {
            Debug.LogError("Cannot find outer polygon vertices! Abort!");
            return null;
        }
        
        LinkedListNode<Vertex> closestIntersectionEdge = null;
        float closestIntersectionDistance = float.PositiveInfinity;
        Vector2 directionVector = Vector2.right;

        //Source: https://rootllama.wordpress.com/2014/06/20/ray-line-segment-intersection-test-in-2d/
        for (int i = 0; i < outerPolygon.Count; i++)
        {
            //Circular extension
            var nextVertex = currentVertex.Next ?? currentVertex.List.First;
            
            //Edge of the outer polygon must be right the x-most vertex of the inner polygon.
            if (currentVertex.Value.position.x <= innerXMost.Value.position.x || nextVertex.Value.position.x <= innerXMost.Value.position.x)
            {
                currentVertex = currentVertex.Next;
                continue;
            }
            
            Vector2 v1 = innerXMost.Value.position - nextVertex.Value.position;
            Vector2 v2 = currentVertex.Value.position - nextVertex.Value.position;
            Vector2 v3 = new Vector2(-directionVector.y, directionVector.x);

            float t1 = Vector3.Cross(v2, v1).magnitude / Vector2.Dot(v2, v3);
            float t2 = Vector2.Dot(v1, v3) / Vector2.Dot(v2, v3);

            if (t1 >= 0 && t2 >= 0 && t2 <= 1)
            {
                pIntersectionPoint = innerXMost.Value.position + directionVector * t1;
                Vector2 intersectionDistance = (Vector2)pIntersectionPoint - innerXMost.Value.position;
                
                if (closestIntersectionEdge == null || intersectionDistance.magnitude < closestIntersectionDistance)
                {
                    closestIntersectionEdge = currentVertex;
                    closestIntersectionDistance = intersectionDistance.magnitude;
                }
            }
            
            currentVertex = currentVertex.Next;
        }

        return closestIntersectionEdge;
    }

    public List<Vector2> GetVertices()
    {
        return new List<Vector2>(_vertices);
    }
}

public static class Triangle
{
    public static float TriangleArea(Vector2 a, Vector2 b, Vector2 c)
    {
        return Math.Abs((a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y)) / 2.0f);
    }

    public static bool IsInTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 point)
    {
        float abcArea = TriangleArea(a, b, c);
        float pbcArea = TriangleArea(point, b, c);
        float pacArea = TriangleArea(a, point, c);
        float pabArea = TriangleArea(a, b, point);
        return abcArea - (pbcArea + pacArea + pabArea) > 0.01f;
    }
}

public static class Vector2Extension
{
    public static float AngleFullDegrees(Vector2 edge1, Vector2 edge2)
    {
        float cross = edge1.x * edge2.y - edge1.y * edge2.x;
        float dot = Vector2.Dot(edge1, edge2);
        float angle = Mathf.Atan2(Mathf.Abs(cross), dot) * Mathf.Rad2Deg;
        if (cross > 0) angle = 360 - angle;
        return angle;
    }
}
