using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>A scroll view whose mouse wheel zooms instead of scrolling (the sphere board, SphereBoardView).</summary>
    public sealed class BoardScrollRect : ScrollRect
    {
        public Action<float> Zoomed;

        public override void OnScroll(PointerEventData data) => Zoomed?.Invoke(data.scrollDelta.y);
    }
}
