// Soap: scene-local opening presentation; depends on Runtime/Core, TMP and Unity UI.
// Only RunController owns gameplay time/phase; references are explicit, no object discovery.
// Handoff: docs/handoffs/Soap.handoff. Rules: root AGENTS.md.
using System;
using System.Collections;
using Regrowth.Core;
using Regrowth.Runtime;
using TMPro;
using UnityEngine;

namespace Regrowth.UI.Intro
{
    // Bootstrap (-300) initializes first. Start runs after ALL OnEnable callbacks,
    // when RunController's isActiveAndEnabled guard is ready, before gameplay Updates.
    [DefaultExecutionOrder(-250)]
    [DisallowMultipleComponent]
    public sealed class OpeningStoryIntro : MonoBehaviour
    {
        [SerializeField, Tooltip("Scene's unique initialized RunController; Intro never writes timeScale.")]
        private RunController runController;
        [SerializeField, Tooltip("Presentation child only; saved active and hidden at completion. Never reference this script's root.")]
        private GameObject introRoot;
        [SerializeField, Tooltip("IntroRoot CanvasGroup; saved alpha 1 for a black first frame.")]
        private CanvasGroup canvasGroup;
        [SerializeField, Tooltip("White TMP child; alpha fades independently from the black background.")]
        private TMP_Text storyTextView;
        [SerializeField, Tooltip("Auto-play once in Start, after all scene components are enabled. Set overrides before Start, or disable auto and call PlayIntro explicitly.")]
        private bool autoPlayOnEnable = true;

        [Header("Story (override > asset > fallback)")]
        [SerializeField, Tooltip("Optional first paragraph asset, read once at playback start; runtime override has priority.")]
        private TextAsset storyTextAsset;
        [SerializeField, TextArea(3, 12), Tooltip("First paragraph when no override/asset is supplied; read at playback start.")]
        private string fallbackText = "A little pup died before he was ready to say goodbye. When he woke up, he was in Hell, surrounded by strange creatures and unfamiliar roads, with no sign of home anywhere.";
        [SerializeField, TextArea(1, 3), Tooltip("Second page, shown after the first fades out while the background remains black. Empty skips this page.")]
        private string closingText = "I want to go home.";
        private string runtimeOverrideText;

        [Header("Unscaled seconds")]
        [SerializeField, Min(0f), Tooltip("Minimum fully visible hold per page, in unscaled seconds; also blank-flow hold.")]
        private float minimumDisplaySeconds = 3f;
        [SerializeField, Min(0f), Tooltip("Maximum hold per page, in unscaled seconds; must be at least the minimum.")]
        private float maximumDisplaySeconds = 12f;
        [SerializeField, Min(0f), Tooltip("Fixed reading allowance per page, in unscaled seconds.")]
        private float baseReadingSeconds = 1f;
        [SerializeField, Min(0f), Tooltip("Additional unscaled hold seconds per non-whitespace character.")]
        private float secondsPerCharacter = 0.07f;
        [SerializeField, Min(0f), Tooltip("Each page's white-text fade-in duration, unscaled seconds.")]
        private float textFadeInSeconds = 0.4f;
        [SerializeField, Min(0f), Tooltip("Each page's white-text fade-out duration, unscaled seconds.")]
        private float textFadeOutSeconds = 0.4f;
        [SerializeField, Min(0f), Tooltip("Final black-background fade-out duration, unscaled seconds.")]
        private float blackFadeOutSeconds = 0.5f;

        private Coroutine playback;
        private bool hasPlayed;
        private bool completed;
        private bool ownsPause;
        private bool interrupted;
        private const float InitializationTimeoutSeconds = 5f;

        /// <summary>Fired once when this scene-local intro finishes or is interrupted.</summary>
        public event Action Completed;

        /// <summary>Main thread, before PlayIntro/automatic Start. Overrides the first paragraph only; null clears, empty skips that page.</summary>
        public void SetStoryText(string text)
        {
            runtimeOverrideText = text;
        }

        /// <summary>Once per component lifetime; repeated calls/enables cannot replay or acquire another pause.</summary>
        public void PlayIntro()
        {
            if (hasPlayed || !isActiveAndEnabled)
            {
                return;
            }

            hasPlayed = true;
            if (runController == null || introRoot == null || canvasGroup == null || storyTextView == null
                || introRoot == gameObject || transform.IsChildOf(introRoot.transform)
                || !canvasGroup.transform.IsChildOf(introRoot.transform)
                || !storyTextView.transform.IsChildOf(introRoot.transform))
            {
                Debug.LogWarning("Opening Story Intro: missing/invalid explicit wiring; skipping intro.", this);
                Finish();
                return;
            }

            introRoot.SetActive(true);
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            storyTextView.alpha = 0f;
            runController.PhaseChanged += OnPhaseChanged;
            playback = StartCoroutine(PlaySequence());
        }

        private void OnEnable()
        {
            // Visual preparation can safely happen early; the phase command cannot.
            if (!hasPlayed)
            {
                if (introRoot != null)
                {
                    introRoot.SetActive(true);
                }
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }
                if (storyTextView != null)
                {
                    storyTextView.alpha = 0f;
                }
            }
        }

        private void Start()
        {
            if (autoPlayOnEnable)
            {
                PlayIntro();
            }
        }

        private IEnumerator PlaySequence()
        {
            float waiting = 0f;
            while (runController != null && (!runController.IsInitialized || !runController.isActiveAndEnabled)
                && waiting < InitializationTimeoutSeconds)
            {
                yield return null;
                waiting += Time.unscaledDeltaTime;
            }

            if (runController == null || !runController.IsInitialized || !runController.isActiveAndEnabled)
            {
                Debug.LogWarning("Opening Story Intro: RunController did not become initialized and active; skipping intro.", this);
                Finish();
                yield break;
            }

            if (runController.Phase == RunPhase.Playing)
            {
                ownsPause = runController.TryPause() && runController.Phase == RunPhase.Paused;
                if (!ownsPause)
                {
                    Debug.LogWarning("Opening Story Intro: unable to own the pause; skipping intro.", this);
                    Finish();
                    yield break;
                }
            }

            string text = runtimeOverrideText ?? (storyTextAsset != null ? storyTextAsset.text : fallbackText);
            int characterCount = CountNonWhitespaceCharacters(text) + CountNonWhitespaceCharacters(closingText);

            // Keep the entire first rendered frame black, even with zero fade durations.
            yield return null;
            if (characterCount == 0)
            {
                Debug.LogWarning("Opening Story Intro: story text is empty; using the minimum black-screen flow.", this);
                yield return Hold(SafeSeconds(minimumDisplaySeconds));
            }
            else
            {
                yield return DisplayText(text);
                yield return DisplayText(closingText);
            }

            yield return Fade(false, 1f, 0f, blackFadeOutSeconds);
            Finish();
        }

        private IEnumerator DisplayText(string text)
        {
            int characterCount = CountNonWhitespaceCharacters(text);
            if (interrupted || characterCount == 0)
            {
                yield break;
            }

            storyTextView.text = text;
            yield return Fade(true, 0f, 1f, textFadeInSeconds);
            float minimum = SafeSeconds(minimumDisplaySeconds);
            float maximum = Mathf.Max(minimum, SafeSeconds(maximumDisplaySeconds));
            float displaySeconds = Mathf.Clamp(SafeSeconds(baseReadingSeconds)
                + characterCount * SafeSeconds(secondsPerCharacter), minimum, maximum);
            yield return Hold(displaySeconds);
            yield return Fade(true, 1f, 0f, textFadeOutSeconds);
        }

        private IEnumerator Hold(float seconds)
        {
            float elapsed = 0f;
            while (!interrupted && elapsed < seconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
        }

        private IEnumerator Fade(bool text, float from, float to, float seconds)
        {
            seconds = SafeSeconds(seconds);
            float elapsed = 0f;
            while (!interrupted && elapsed < seconds)
            {
                SetAlpha(text, Mathf.Lerp(from, to, elapsed / seconds));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            SetAlpha(text, to);
        }

        private void SetAlpha(bool text, float alpha)
        {
            if (text && storyTextView != null)
            {
                storyTextView.alpha = alpha;
            }
            else if (!text && canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }
        }

        private void OnPhaseChanged(RunPhase phase)
        {
            // Another transition cancels our ownership. Never resume an unrelated later pause,
            // a choice transaction or death; never issue phase commands inside this notification.
            if (ownsPause && phase != RunPhase.Paused)
            {
                ownsPause = false;
                interrupted = true;
            }
        }

        private void Finish()
        {
            if (!hasPlayed || completed)
            {
                return;
            }

            completed = true;
            playback = null;
            if (runController != null)
            {
                runController.PhaseChanged -= OnPhaseChanged;
            }
            if (introRoot != null && introRoot != gameObject && !transform.IsChildOf(introRoot.transform))
            {
                introRoot.SetActive(false);
            }

            bool release = ownsPause;
            ownsPause = false;
            if (release && runController != null && runController.IsInitialized
                && runController.Phase == RunPhase.Paused)
            {
                runController.TryResume();
            }

            Completed?.Invoke();
        }

        private void OnDisable()
        {
            if (playback != null)
            {
                StopCoroutine(playback);
            }
            Finish();
        }

        private static int CountNonWhitespaceCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            int count = 0;
            foreach (char character in text)
            {
                if (!char.IsWhiteSpace(character))
                {
                    count++;
                }
            }
            return count;
        }

        private static float SafeSeconds(float seconds)
        {
            return float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0f : Mathf.Max(0f, seconds);
        }
    }
}
