using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class EarClipping
{
    public LinkedList<Vertex> vertices;
    public List<Vector2> straightList;

    public void SetupClipping(Polygon pPolygon)
    {
        int index = 0;
        vertices = new LinkedList<Vertex>();
        straightList = new List<Vector2>();
        foreach (var currentVertex in pPolygon.polygon)
        {
            vertices.AddLast(new Vertex(currentVertex, index++));
            straightList.Add(currentVertex);
        }

        List<Polygon> availablePolygons = pPolygon.GetInnerPolygons();
        if (availablePolygons != null)
        {
            while (availablePolygons.Count > 0)
            {
                //1. Look for right most polygon
                Polygon xMostPolygon = null;
                LinkedListNode<Vector2> xMostValue = null;
                foreach (var currentPolygon in availablePolygons)
                {
                    LinkedListNode<Vector2> currentXMostValue = currentPolygon.FindXMostVertex();
                    if (xMostPolygon == null || currentXMostValue.Value.x > xMostValue.Value.x)
                    {
                        xMostPolygon = currentPolygon;
                        xMostValue = currentXMostValue;
                    }
                }

                if (xMostPolygon == null) throw new ArgumentException("No right-most polygon found!");

                //Add to list first
                LinkedList<Vertex> innerPolygon = new LinkedList<Vertex>();
                LinkedListNode<Vertex> innerXMost = null;

                for (LinkedListNode<Vector2> currentNode = xMostPolygon.polygon.First;
                    currentNode != null;
                    currentNode = currentNode.Next)
                {
                    LinkedListNode<Vertex> newVertex =
                        new LinkedListNode<Vertex>(new Vertex(currentNode.Value, index++));
                    straightList.Add(currentNode.Value);
                    if (currentNode == xMostValue) innerXMost = newVertex;
                    innerPolygon.AddLast(newVertex);
                }

                Vertex innerXMostCopy = new Vertex(innerXMost.Value.position, index++);
                straightList.Add(innerXMostCopy.position);
                LinkedListNode<Vertex> mutuallyVisible = FindMutuallyVisibleVertex(innerXMost);
                Vertex mutuallyVisibleCopy = new Vertex(mutuallyVisible.Value.position, index++);
                straightList.Add(mutuallyVisibleCopy.position);

                //Add together
                bool notOnceRan = true;
                LinkedListNode<Vertex> startNode = vertices.Find(mutuallyVisible.Value);

                for (LinkedListNode<Vertex> currentNode = innerXMost;
                    currentNode != innerXMost || notOnceRan;
                    currentNode = currentNode.Next ?? currentNode.List.First)
                {
                    notOnceRan = false;
                    LinkedListNode<Vertex> newNode = new LinkedListNode<Vertex>(currentNode.Value);
                    vertices.AddAfter(startNode, newNode);
                    startNode = newNode;
                }

                vertices.AddAfter(startNode, innerXMostCopy);
                startNode = vertices.Find(innerXMostCopy);
                vertices.AddAfter(startNode, mutuallyVisibleCopy);
                
                Debug.LogError($"inner x most was {innerXMostCopy.position}");
                Debug.LogError($"mutually visible was {mutuallyVisibleCopy.position}");

                availablePolygons.Remove(xMostPolygon);
            }
        }


        //Then check if vertices are convex.
        for (LinkedListNode<Vertex> current = vertices.First; current != null; current = current.Next)
        {
            var previous = current.Previous ?? current.List.Last;
            var next = current.Next ?? current.List.First;
            current.Value.isConvex = IsConvex(previous.Value.position, current.Value.position, next.Value.position);
        }

        foreach (var vertex in vertices)
        {
            Debug.LogWarning($"Vertex {vertex.index} at position {vertex.position} and IsConvex is {vertex.isConvex}");
        }
    }

    public int[] Triangulate()
    {
        int[] tris = new int[(vertices.Count - 2) * 3];
        int trisIndex = 0;
        int failSave = 0;

        while (vertices.Count >= 3)
        {
            for (LinkedListNode<Vertex> currentVertexNode = vertices.First;
                currentVertexNode != null;
                currentVertexNode = currentVertexNode.Next)
            {
                var previous = currentVertexNode.Previous ?? currentVertexNode.List.Last;
                var next = currentVertexNode.Next ?? currentVertexNode.List.First;
                if (!currentVertexNode.Value.isConvex) continue;
                if (!IsEar(previous.Value.position, currentVertexNode.Value.position, next.Value.position)) continue;
                vertices.Remove(currentVertexNode);

                //Create triangle
                tris[trisIndex * 3] = previous.Value.index;
                tris[trisIndex * 3 + 1] = currentVertexNode.Value.index;
                tris[trisIndex * 3 + 2] = next.Value.index;
                trisIndex += 1;

                //Check if previous and next vertices are reflex and if so; recalculate
                if (!previous.Value.isConvex)
                {
                    bool convex = IsConvex((previous.Previous ?? previous.List.Last).Value.position,
                        previous.Value.position, next.Value.position);
                    previous.Value.isConvex = convex;
                }

                if (!next.Value.isConvex)
                {
                    bool convex = IsConvex(previous.Value.position, next.Value.position,
                        (next.Next ?? next.List.First).Value.position);
                    next.Value.isConvex = convex;
                }

                break;
            }

            if (failSave++ > 5000)
            {
                Debug.LogError("FAILSAFE");
                break;
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

    private LinkedListNode<Vertex> FindMutuallyVisibleVertex(LinkedListNode<Vertex> innerXMost)
    {
        Vector2? intersectionPoint = null;
        LinkedListNode<Vertex> closestIntersectionEdge = FindClosestIntersectionEdge(ref intersectionPoint, innerXMost);
        //Find best-edge x-most point
        var secondPointOfEdge = closestIntersectionEdge.Next ?? closestIntersectionEdge.List.First;
        LinkedListNode<Vertex> mutuallyVisibleVertex = closestIntersectionEdge.Value.position.x > secondPointOfEdge.Value.position.x ? closestIntersectionEdge : secondPointOfEdge;
        //This verifies whether there are vertices in the triangle created by the inner x-most point, the intersection point with the outer polygon
        //and the mutually visible vertex. Since these vertices can potentially occlude the mutually visible vertex.
        float currentShortestAngle = float.PositiveInfinity;
        for (LinkedListNode<Vertex> outerVertex = vertices.First; outerVertex != null; outerVertex = outerVertex.Next)
        {
            if (intersectionPoint == null)
            {
                throw new ArgumentException("No intersection-point found.");
            }
            
            if(outerVertex.Value.position == innerXMost.Value.position || outerVertex.Value.position == (Vector2) intersectionPoint|| outerVertex.Value.position == mutuallyVisibleVertex.Value.position)
                continue;

            if (Triangle.IsInTriangle(innerXMost.Value.position, (Vector2) intersectionPoint, mutuallyVisibleVertex.Value.position, outerVertex.Value.position))
            {
                //Take the one with the smallest angle
                float currentAngle = Vector2Extension.AngleFullDegrees(
                    (Vector2) intersectionPoint - innerXMost.Value.position,
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

    private LinkedListNode<Vertex> FindClosestIntersectionEdge(ref Vector2? pIntersectionPoint, LinkedListNode<Vertex> innerXMost)
    {
        LinkedListNode<Vertex> currentVertex = vertices.First;

        if (currentVertex == null)
        {
            Debug.LogError("Cannot find outer polygon vertices! Abort!");
            return null;
        }

        LinkedListNode<Vertex> closestIntersectionEdge = null;
        float closestIntersectionDistance = float.PositiveInfinity;
        Vector2 directionVector = Vector2.right;

        //Source: https://rootllama.wordpress.com/2014/06/20/ray-line-segment-intersection-test-in-2d/
        for (int i = 0; i < vertices.Count; i++)
        {
            //Circular extension
            var nextVertex = currentVertex.Next ?? currentVertex.List.First;

            //Edge of the outer polygon must be right the x-most vertex of the inner polygon.
            if (currentVertex.Value.position.x <= innerXMost.Value.position.x ||
                nextVertex.Value.position.x <= innerXMost.Value.position.x)
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
                Vector2 intersectionDistance = (Vector2) pIntersectionPoint - innerXMost.Value.position;

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
    public readonly LinkedList<Vector2> polygon;
    private List<Polygon> innerPolygons;

    public List<Polygon> GetInnerPolygons()
    {
        return innerPolygons != null ? new List<Polygon>(innerPolygons) : null;
    }

    public Polygon(List<Vector2> pOuterPolygon)
    {
        polygon = new LinkedList<Vector2>(pOuterPolygon);
    }

    public void AddInnerPolygon(Polygon pInnerPolygonVertices)
    {
        if (innerPolygons == null)
            innerPolygons = new List<Polygon>();
        innerPolygons.Add(pInnerPolygonVertices);
    }

    public LinkedListNode<Vector2> FindXMostVertex()
    {
        //This searches for the vertex with the highest values on the x-axis.
        LinkedListNode<Vector2> currentBest = null;
        for (LinkedListNode<Vector2> currentVertex = polygon.First;
            currentVertex != null;
            currentVertex = currentVertex.Next)
        {
            if (currentBest == null || currentVertex.Value.x > currentBest.Value.x)
            {
                currentBest = currentVertex;
            }
        }

        return currentBest;
    }
}

public static class Triangle
{
    public static float TriangleArea(Vector2 a, Vector2 b, Vector2 c)
    {
        return Mathf.Abs((a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y)) / 2.0f);
    }

    public static bool IsInTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 point)
    {
        float abcArea = TriangleArea(a, b, c);
        float pbcArea = TriangleArea(point, b, c);
        float pacArea = TriangleArea(a, point, c);
        float pabArea = TriangleArea(a, b, point);
        return Mathf.Abs(abcArea - (pbcArea + pacArea + pabArea)) < 0.01f;
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