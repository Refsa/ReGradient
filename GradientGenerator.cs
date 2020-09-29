using System.Collections.Generic;
using UnityEngine;

namespace Refsa.ReGradient
{
    [System.Serializable]
    public struct ReGradientNode
    {
        public Color Color;
        public float Percent;
        public int ID;
    }

    [System.Serializable]
    public struct ReGradientData
    {
        public List<ReGradientNode> Nodes;
        public Vector2Int Size;

        public bool HasValue => Nodes != null && Nodes.Count > 0;

        public void AddNode(Color color, float percent)
        {
            var node = new ReGradientNode { Color = color, Percent = percent, ID = Random.Range(9999, 99999999) };
            Nodes.Add(node);
        }
    }
}