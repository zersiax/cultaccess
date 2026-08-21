using CultAccess.Audio;
using CultAccess.Localization;
using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.UI
{
    /// <summary>
    /// The mod's own settings menu: spoken, keyboard-driven, and drawing nothing on screen.
    ///
    /// There is no Unity canvas here, deliberately. The audience cannot see one, building one
    /// would mean adopting the game's UI framework and its focus rules, and the announcement
    /// path this mod already has is a better interface than any widget tree read through a
    /// screen reader. What this needed instead was a page model, a cursor, and wording — all
    /// of which are pure and tested offline.
    ///
    /// The game keeps running while the menu is open. Nothing here pauses, freezes or slows
    /// it: doing so would hand the player a pause that a sighted player does not get in a
    /// dungeon, which is a change to the game rather than access to it. The consequence is
    /// that the menu is something you open when you are safe, and the opening announcement
    /// says so.
    ///
    /// Arrow keys, Enter and Backspace are all in the safe pool derived from the game's own
    /// Rewired dump, so none of them can reach gameplay. Movement stays on W A S D throughout,
    /// which is also why the menu does not use those.
    /// </summary>
    internal static class ConfigMenu
    {
        /// <summary>
        /// How long after focus lands on a learn-sounds row before its cue plays. Long enough
        /// that the spoken name is not talked over by the sound it is naming, short enough
        /// that the two are still obviously about each other.
        /// </summary>
        private const float FocusPreviewDelay = 0.6f;

        private static readonly ConfigMenuNavigator Navigator = new ConfigMenuNavigator();

        private static float _focusPreviewAt = float.PositiveInfinity;
        private static ConfigItem _pendingPreviewItem;

        /// <summary>While open, the mod's world hotkeys belong to the menu.</summary>
        internal static bool IsOpen => Navigator.IsOpen;

        internal static void Toggle()
        {
            if (Navigator.IsOpen)
            {
                Close();
                return;
            }

            Open();
        }

        internal static void Open()
        {
            var root = ConfigMenuTree.Root();
            Navigator.Open(root);

            Speaker.Say(ConfigMenuText.PageEntry(root));
            Speaker.Say(ConfigMenuText.Controls(CloseKeyName()), SpeechPriority.Queued);
            ScheduleFocusPreview();
        }

        internal static void Close()
        {
            if (!Navigator.IsOpen) return;

            CancelFocusPreview();
            CuePreview.Cancel();
            Navigator.Close();
            Speaker.Say(Strings.Get("config.closed"));
        }

        /// <summary>Re-read the focused row, for the where-am-I key.</summary>
        internal static bool AnnounceCurrent()
        {
            if (!Navigator.IsOpen) return false;

            Speaker.Say(ConfigMenuText.Focus(Navigator.Page));
            return true;
        }

        /// <summary>Explain the focused row, for the help key.</summary>
        internal static bool AnnounceHelp()
        {
            if (!Navigator.IsOpen) return false;

            Speaker.Say(ConfigMenuText.Controls(CloseKeyName()));
            Speaker.Say(ConfigMenuText.Detail(Navigator.Current), SpeechPriority.Queued);
            return true;
        }

        /// <summary>
        /// Handle the menu's own keys. Driven from the plugin's update before any world
        /// hotkey is considered, so a key cannot act in two places on the same frame.
        /// </summary>
        internal static void Tick()
        {
            CuePreview.Tick();

            if (!Navigator.IsOpen) return;

            RunDueFocusPreview();

            if (Input.Keys.Down(KeyCode.UpArrow)) MoveFocus(-1);
            else if (Input.Keys.Down(KeyCode.DownArrow)) MoveFocus(1);
            else if (Input.Keys.Down(KeyCode.Home)) MoveFocusTo(0);
            else if (Input.Keys.Down(KeyCode.End)) MoveFocusTo(Navigator.Page.Items.Count - 1);
            else if (Input.Keys.Down(KeyCode.LeftArrow)) AdjustFocus(-1);
            else if (Input.Keys.Down(KeyCode.RightArrow)) AdjustFocus(1);
            else if (Input.Keys.Down(KeyCode.Return) || Input.Keys.Down(KeyCode.KeypadEnter))
                ActivateFocus();
            else if (Input.Keys.Down(KeyCode.Backspace)) GoBack();
        }

        internal static void Shutdown()
        {
            CancelFocusPreview();

            // Before anything releases the sounds themselves. A preview can be holding a
            // sustained channel open, and stopping it after its sound has been freed is the
            // one ordering mistake here that is audible rather than merely wrong.
            CuePreview.Cancel();

            Navigator.Close();
            ConfigMenuTree.Invalidate();
        }

        private static void MoveFocus(int delta)
        {
            if (!Navigator.Move(delta)) return;

            CuePreview.Cancel();
            Speaker.Say(ConfigMenuText.Focus(Navigator.Page));
            ScheduleFocusPreview();
        }

        private static void MoveFocusTo(int index)
        {
            if (!Navigator.MoveTo(index)) return;

            CuePreview.Cancel();
            Speaker.Say(ConfigMenuText.Focus(Navigator.Page));
            ScheduleFocusPreview();
        }

        private static void AdjustFocus(int direction)
        {
            var item = Navigator.Current;
            if (item == null) return;

            if (!Navigator.Adjust(direction))
            {
                // Already at the end of the range, or a row with nothing to adjust. Saying
                // the value again is the honest answer: silence would read as a dead key.
                Speaker.Say(ConfigMenuText.Changed(item));
                return;
            }

            Speaker.Say(ConfigMenuText.Changed(item));
        }

        private static void ActivateFocus()
        {
            var item = Navigator.Current;
            var result = Navigator.Activate();

            switch (result)
            {
                case ConfigActivation.EnteredSubmenu:
                    CuePreview.Cancel();
                    Speaker.Say(ConfigMenuText.PageEntry(Navigator.Page));
                    ScheduleFocusPreview();
                    break;

                case ConfigActivation.Changed:
                    Speaker.Say(ConfigMenuText.Changed(item));
                    break;

                case ConfigActivation.Invoked:
                    // The action is its own feedback: a preview plays the sound, and saying
                    // its name over the top would defeat the purpose of playing it.
                    CancelFocusPreview();
                    break;

                default:
                    Speaker.Say(Strings.Get("config.nothing_to_do"));
                    break;
            }
        }

        private static void GoBack()
        {
            CuePreview.Cancel();

            if (!Navigator.Back())
            {
                Close();
                return;
            }

            Speaker.Say(ConfigMenuText.PageEntry(Navigator.Page));
            ScheduleFocusPreview();
        }

        /// <summary>
        /// Arm the focused row's own preview, if it has one. Delayed rather than immediate so
        /// the cue does not play underneath the sentence naming it.
        /// </summary>
        private static void ScheduleFocusPreview()
        {
            var item = Navigator.Current;
            if (item?.OnFocus == null)
            {
                CancelFocusPreview();
                return;
            }

            _pendingPreviewItem = item;
            _focusPreviewAt = Time.unscaledTime + FocusPreviewDelay;
        }

        private static void RunDueFocusPreview()
        {
            if (_pendingPreviewItem == null || Time.unscaledTime < _focusPreviewAt) return;

            var item = _pendingPreviewItem;
            CancelFocusPreview();
            item.OnFocus();
        }

        private static void CancelFocusPreview()
        {
            _pendingPreviewItem = null;
            _focusPreviewAt = float.PositiveInfinity;
        }

        private static string CloseKeyName() =>
            Plugin.KeyConfigMenu == null ? "F2" : Plugin.KeyConfigMenu.Value.ToString();
    }
}
