using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa un trazo individual en el tablero de dibujo.
/// Se serializa a/desde JSON para sincronización por Normcore.
/// </summary>
[Serializable]
public class DrawingStroke
{
    public List<StrokePoint> points = new List<StrokePoint>();
    public string colorHex;  // "#FF0000" (rojo) o "#000000" (negro)
    public float  lineWidth;
}

[Serializable]
public struct StrokePoint
{
    public float x, y, z;
    public StrokePoint(Vector3 v) { x = v.x; y = v.y; z = v.z; }
    public Vector3 ToVector3() => new Vector3(x, y, z);
}

/// <summary>
/// Contenedor de todos los trazos para serialización JSON.
/// </summary>
[Serializable]
public class StrokesData
{
    public List<DrawingStroke> strokes = new List<DrawingStroke>();
}
