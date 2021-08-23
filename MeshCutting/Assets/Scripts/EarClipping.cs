using System;
using System.Collections.Generic;
using UnityEngine;

public class EarClipping
{
    private LinkedList<Vertex> _vertices;
    public List<Vector2> originalVertices;

    public void SetupClipping(Polygon pPolygon)
    {
        int index = 0;
        _vertices = new LinkedList<Vertex>();
        originalVertices = new List<Vector2>();
        
        foreach (var currentVertex in pPolygon.polygon)
        {
            _vertices.AddLast(new Vertex(currentVertex, index++));
            originalVertices.Add(currentVertex);
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
                
                LinkedListNode<Vertex> mutuallyVisible = FindMutuallyVisibleVertex(xMostValue);

                bool notOnceRan = true;
                LinkedListNode<Vertex> innerXMost = null;
                LinkedListNode<Vertex> startNode = _vertices.Find(mutuallyVisible.Value);
                
                for (LinkedListNode<Vector2> currentNode = xMostValue;
                    currentNode != xMostValue || notOnceRan;
                    currentNode = currentNode.Next ?? currentNode.List.First)
                {
                    LinkedListNode<Vertex> newVertex = new LinkedListNode<Vertex>(new Vertex(currentNode.Value, index++));
                    originalVertices.Add(currentNode.Value);
                    _vertices.AddAfter(startNode, newVertex);
                    startNode = newVertex;
                    if (notOnceRan)
                    {
                        innerXMost = newVertex;
                        notOnceRan = false;
                    }
                }
                
                Vertex innerXMostCopy = new Vertex(innerXMost.Value.position, index++);
                originalVertices.Add(innerXMostCopy.position);
                
                Vertex mutuallyVisibleCopy = new Vertex(mutuallyVisible.Value.position, index++);
                originalVertices.Add(mutuallyVisibleCopy.position);
                
                _vertices.AddAfter(startNode, innerXMostCopy);
                _vertices.AddAfter(_vertices.Find(innerXMostCopy), mutuallyVisibleCopy);

                availablePolygons.Remove(xMostPolygon);
            }
        }

        //Then check if vertices are convex.
        for (LinkedListNode<Vertex> current = _vertices.First; current != null; current = current.Next)
        {
            var previous = current.Previous ?? current.List.Last;
            var next = current.Next ?? current.List.First;
            current.Value.isConvex = IsConvex(previous.Value.position, current.Value.position, next.Value.position);
        }
    }

    public int[] Triangulate()
    {
        int[] tris = new int[(_vertices.Count - 2) * 3];
        int trisIndex = 0;

        while (_vertices.Count >= 3)
        {
            bool earFound = false;
            
            for (LinkedListNode<Vertex> currentVertexNode = _vertices.First; currentVertexNode != null;
                currentVertexNode = currentVertexNode.Next)
            {
                var previous = currentVertexNode.Previous ?? currentVertexNode.List.Last;
                var next = currentVertexNode.Next ?? currentVertexNode.List.First;
                if (!currentVertexNode.Value.isConvex) continue;
                if (!IsEar(previous.Value, currentVertexNode.Value, next.Value)) continue;
                
                earFound = true;
                _vertices.Remove(currentVertexNode);

                //Check if previous and next vertices are reflex and if so; recalculate
                if (!previous.Value.isConvex)
                {
                    previous.Value.isConvex = IsConvex((previous.Previous ?? previous.List.Last).Value.position,
                        previous.Value.position, next.Value.position);
                }

                if (!next.Value.isConvex)
                {
                    next.Value.isConvex = IsConvex(previous.Value.position, next.Value.position,
                        (next.Next ?? next.List.First).Value.position);
                }

                //Create triangle
                tris[trisIndex * 3] = previous.Value.index;
                tris[trisIndex * 3 + 1] = currentVertexNode.Value.index;
                tris[trisIndex * 3 + 2] = next.Value.index;
                trisIndex += 1;


                break;
            }

            if (!earFound)
            {
                Debug.LogError("No ear found!");
                return null;
            }
        }

        return tris;
    }

    private bool IsEar(Vertex previous, Vertex current, Vertex next)
    {
        //This checks for a possible ear.
        foreach (var vertex in _vertices)
        {
            if (vertex.isConvex) continue;
            if (previous.index == vertex.index || current.index == vertex.index || next.index == vertex.index) continue;
            if (Triangle.IsInTriangle(previous.position, current.position, next.position, vertex.position))
                return false;
        }

        return true;
    }

    private bool IsConvex(Vector2 previous, Vector2 current, Vector2 next)
    {
        //Convex or Reflex?
        Vector2 edge1 = (next - current);
        Vector2 edge2 = (previous - current);
        return Vector2Extension.AngleFullDegrees(edge1, edge2) < 180.0f;
    }

    private LinkedListNode<Vertex> FindMutuallyVisibleVertex(LinkedListNode<Vector2> innerXMost)
    {
        Vector2? intersectionPoint = null;
        LinkedListNode<Vertex> closestIntersectionEdge = FindClosestIntersectionEdge(ref intersectionPoint, innerXMost);
        //Find best-edge x-most point
        var secondPointOfEdge = closestIntersectionEdge.Next ?? closestIntersectionEdge.List.First;
        LinkedListNode<Vertex> mutuallyVisibleVertex = closestIntersectionEdge.Value.position.x > secondPointOfEdge.Value.position.x ?
            closestIntersectionEdge : secondPointOfEdge;
        
        
        //This verifies whether there are vertices in the triangle created by the inner x-most point, the intersection point with the outer polygon
        //and the mutually visible vertex. Since these vertices can potentially occlude the mutually visible vertex.
        float currentShortestAngle = float.PositiveInfinity;
        float currentShortestDistance = 0;
        LinkedListNode<Vertex> tester = null;
        for (LinkedListNode<Vertex> outerVertex = _vertices.First; outerVertex != null; outerVertex = outerVertex.Next)
        {
            if (intersectionPoint == null)
            {
                throw new ArgumentException("No intersection-point found.");
            }
            
            if(outerVertex == mutuallyVisibleVertex || outerVertex.Value.position == mutuallyVisibleVertex.Value.position) continue;

            if (Triangle.IsInTriangle(innerXMost.Value, (Vector2) intersectionPoint, mutuallyVisibleVertex.Value.position, outerVertex.Value.position))
            {
                //Take the one with the smallest angle
                float currentAngle = Vector2Extension.AngleFullDegrees((Vector2) intersectionPoint - innerXMost.Value, 
                    outerVertex.Value.position - innerXMost.Value);
                
                if (currentAngle < currentShortestAngle)
                {
                    tester = outerVertex;
                    currentShortestAngle = currentAngle;
                    currentShortestDistance = (outerVertex.Value.position - innerXMost.Value).magnitude;
                }
                else if (Mathf.Abs(currentAngle - currentShortestAngle) < 0.01f)
                {
                    float currentDistance = (outerVertex.Value.position - innerXMost.Value).magnitude;
                    if (currentAngle < currentShortestDistance)
                    {
                        tester = outerVertex;
                        currentShortestDistance = currentDistance;
                    }
                }
            }
        }

        if (tester != null) mutuallyVisibleVertex = tester;
        return mutuallyVisibleVertex;
    }

    private LinkedListNode<Vertex>FindClosestIntersectionEdge(ref Vector2? pIntersectionPoint, LinkedListNode<Vector2> innerXMost)
    {
        LinkedListNode<Vertex> currentVertex = _vertices.First;

        if (currentVertex == null)
        {
            Debug.LogError("Cannot find outer polygon vertices! Abort!");
            return null;
        }

        LinkedListNode<Vertex> closestIntersectionEdge = null;
        float closestIntersectionDistance = float.PositiveInfinity;
        Vector2 directionVector = Vector2.right;
        
        for (int i = 0; i < _vertices.Count; i++)
        {
            //Circular extension
            var nextVertex = currentVertex.Next ?? currentVertex.List.First;

            //Edge of the outer polygon must be right the x-most vertex of the inner polygon.
            if (currentVertex.Value.position.x <= innerXMost.Value.x && nextVertex.Value.position.x <= innerXMost.Value.x)
            {
                currentVertex = currentVertex.Next;
                continue;
            }

            if (innerXMost.Value.y > currentVertex.Value.position.y ==
                innerXMost.Value.y > nextVertex.Value.position.y)
            {
                currentVertex = currentVertex.Next;
                continue;
            }

            Vector2 v1 = (innerXMost.Value - nextVertex.Value.position);
            Vector2 v2 = (currentVertex.Value.position - nextVertex.Value.position);
            Vector2 v3 = new Vector2(-directionVector.y, directionVector.x);
            
            
            float t1 = Vector3.Cross(v2, v1).magnitude / Vector2.Dot(v2, v3);
            float t2 = Vector2.Dot(v1, v3) / Vector2.Dot(v2, v3);
            
            if (t1 >= 0 && t2 >= 0 && t2 <= 1)
            {
                
                pIntersectionPoint = innerXMost.Value + directionVector * t1;
                float intersectionDistance = ((Vector2) pIntersectionPoint - innerXMost.Value).magnitude;

                if (closestIntersectionEdge == null || intersectionDistance < closestIntersectionDistance)
                {
                    closestIntersectionEdge = currentVertex;
                    closestIntersectionDistance = intersectionDistance;
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
    private List<Polygon> _innerPolygons;

    public List<Polygon> GetInnerPolygons()
    {
        return _innerPolygons != null ? new List<Polygon>(_innerPolygons) : null;
    }

    public Polygon(List<Vector2> pOuterPolygon)
    {
        polygon = new LinkedList<Vector2>(pOuterPolygon);
    }

    public void AddInnerPolygon(Polygon pInnerPolygonVertices)
    {
        _innerPolygons ??= new List<Polygon>();
        _innerPolygons.Add(pInnerPolygonVertices);
    }

    public LinkedListNode<Vector2> FindXMostVertex()
    {
        //This searches for the vertex with the highest values on the x-axis.
        LinkedListNode<Vector2> currentBest = null;
        for (LinkedListNode<Vector2> currentVertex = polygon.First; currentVertex != null; currentVertex = currentVertex.Next)
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
    private static float TriangleArea(Vector2 a, Vector2 b, Vector2 c)
    {
        return Mathf.Abs((a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y)) / 2.0f);
    }

    public static bool IsInTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 point)
    {
        float abcArea = TriangleArea(a, b, c);
        float pbcArea = TriangleArea(point, b, c);
        float pacArea = TriangleArea(a, point, c);
        float pabArea = TriangleArea(a, b, point);
        return Mathf.Abs(abcArea - (pbcArea + pacArea + pabArea)) < 0.00001f;
    }
}

public static class Vector2Extension
{
    public static float AngleFullDegrees(Vector2 edge1, Vector2 edge2)
    {
        edge1.Normalize();
        edge2.Normalize();
        float cross = edge1.x * edge2.y - edge1.y * edge2.x;
        float dot = Vector2.Dot(edge1, edge2);
        float angle = Mathf.Atan2(Mathf.Abs(cross), dot) * Mathf.Rad2Deg;
        if (cross > 0) angle = 360 - angle;
        return angle;
    }
}