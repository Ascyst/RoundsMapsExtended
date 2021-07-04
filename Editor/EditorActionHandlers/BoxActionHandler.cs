﻿using UnityEngine;

namespace MapsExtended.Editor
{
    public class BoxActionHandler : MonoBehaviour, IEditorActionHandler
    {
        public bool Resize(Vector3 mouseDelta, int resizeDirection)
        {
            float gridSize = this.gameObject.GetComponentInParent<Editor.MapEditor>().gridSize;

            var scaleMulti = Editor.TogglePosition.directionMultipliers[resizeDirection];

            var snappedRotation = this.transform.rotation;
            snappedRotation.eulerAngles = new Vector3(0, 0, Editor.EditorUtils.Snap(snappedRotation.eulerAngles.z, 180));

            var scaleDelta = Vector3.Scale(snappedRotation * mouseDelta, new Vector3(scaleMulti.x, scaleMulti.y, 0));
            var positionDelta = this.transform.rotation * Vector3.Scale(snappedRotation * mouseDelta, new Vector3(Mathf.Abs(scaleMulti.x), Mathf.Abs(scaleMulti.y), 0));

            var oldScale = this.transform.localScale;
            var newScale = this.transform.localScale + scaleDelta;
            if ((newScale.x < oldScale.x && newScale.x < gridSize) || (newScale.y < oldScale.y && newScale.y < gridSize))
            {
                return false;
            }

            this.transform.localScale = newScale;
            this.transform.position += positionDelta * 0.5f;
            return true;
        }
    }
}