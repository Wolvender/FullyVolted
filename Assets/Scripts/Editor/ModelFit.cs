using UnityEngine;

namespace FullyVolted.EditorTools
{
    // Downloaded models come in any unit and with their pivot anywhere. This scales one to a known size and
    // centres it inside a wrapper object, so the wrapper can be placed and given a collider like a primitive.
    internal static class ModelFit
    {
        // Instantiates nothing: `model` must already be a child of `root`, and `root` must sit at the origin
        // with no rotation (so renderer bounds are the model's own). Returns the fitted size, for a collider.
        public static Vector3 FitCentered(GameObject model, float longestSide)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var scale = longestSide / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = -bounds.center * scale;
            return bounds.size * scale;
        }
    }
}
