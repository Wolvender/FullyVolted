using UnityEngine;

namespace FullyVolted.Feedback
{
    // Fades a CanvasGroup in while the player looks at it, and out again when they look away.
    // Optionally switches off a hint (the "look at your hand" sign) the first time it has been seen.
    [RequireComponent(typeof(CanvasGroup))]
    public class GazeFade : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [Tooltip("Within this angle of the view direction counts as looking at it.")]
        [SerializeField] private float lookAngle = 30f;
        [SerializeField] private float maxDistance = 1.1f;
        [SerializeField] private float fadeSpeed = 8f;
        [Tooltip("Switched off the first time the panel is fully shown. Optional.")]
        [SerializeField] private GameObject hideOnFirstReveal;

        private CanvasGroup group;
        private bool revealedOnce;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
        }

        private void Update()
        {
            if (head == null)
                return;

            var toPanel = transform.position - head.position;
            bool looking = toPanel.magnitude <= maxDistance &&
                           Vector3.Angle(head.forward, toPanel) <= lookAngle;

            group.alpha = Mathf.MoveTowards(group.alpha, looking ? 1f : 0f, fadeSpeed * Time.deltaTime);

            if (!revealedOnce && group.alpha >= 1f)
            {
                revealedOnce = true;
                if (hideOnFirstReveal != null)
                    hideOnFirstReveal.SetActive(false);
            }
        }
    }
}
