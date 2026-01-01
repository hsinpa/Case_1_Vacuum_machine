using System;
using NUnit.Framework;
using UnityEngine;

namespace Hsinpa.Map
{
    public class GridMap2D : MonoBehaviour
    {
        [SerializeField, UnityEngine.Range(1, 100)]
        private int gridSizeX = 1;

        [SerializeField, UnityEngine.Range(1, 100)]
        private int gridSizeY = 1;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(gridSizeX, gridSizeY, 0));
        }

    }
}
