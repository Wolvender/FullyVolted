using UnityEngine;

namespace FullyVolted.Climbing
{
    // Draws the lanyard from the harness to its carabiner, and turns it red while over-stretched, so the
    // climber can see which hook needs moving up.
    [RequireComponent(typeof(LineRenderer))]
    public class LanyardLine : MonoBehaviour
    {
        private LineRenderer line;

        [SerializeField] private Carabiner carabiner;
        [SerializeField] private Color slackColor = new Color(0.95f, 0.75f, 0.15f);
        [SerializeField] private Color overStretchedColor = new Color(1f, 0.15f, 0.1f);

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
        }

        private void LateUpdate()
        {
            if (carabiner == null || carabiner.Harness == null)
                return;

            line.SetPosition(0, carabiner.Harness.position);
            line.SetPosition(1, carabiner.transform.position);

            var color = carabiner.IsOverExtended ? overStretchedColor : slackColor;
            line.startColor = color;
            line.endColor = color;
        }
    }
}
