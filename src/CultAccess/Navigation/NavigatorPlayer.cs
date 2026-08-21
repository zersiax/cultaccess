using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Finds the player's transform, whichever controller currently owns them.
    ///
    /// <c>PlayerFarming.Instance</c> is null during the intro, where the Lamb is driven by
    /// <c>PlayerPrisonerController</c> instead — keying off PlayerFarming alone reports
    /// "not in the world" while standing in an obviously loaded scene. The "Player" tag is
    /// what the game's own code uses when it needs the player regardless of controller, so
    /// it is the better primary lookup.
    ///
    /// Shared by navigation, the audio beacon and the enemy radar, all of which need a
    /// reference point and must agree on it.
    /// </summary>
    public static class NavigatorPlayer
    {
        private static Transform _cached;
        private static Interactor _cachedInteractor;
        private static float _nextMissRetryAt;

        /// <summary>
        /// How long to wait before searching again after a search that found nothing.
        ///
        /// A hit is cached, so the fast path costs a null check. A **miss** costs three
        /// scene-wide searches — a tag lookup, a MonoSingleton whose empty cache falls back to
        /// FindObjectOfType, and a FindObjectOfType of its own — and there is no cache for
        /// "still absent". Callers in the update loop therefore paid all three every frame
        /// for as long as there was no player, and the cost grew as the scene filled up,
        /// which is exactly what the frame budget recorded: a steady climb from one
        /// millisecond a frame to over three.
        ///
        /// A quarter of a second bounds that to four searches a second. Nothing needs the
        /// player faster than that at a moment when there is not one.
        /// </summary>
        private const float MissRetrySeconds = 0.25f;

        public static Transform Resolve()
        {
            if (_cached != null && _cached.gameObject.activeInHierarchy) return _cached;

            if (Time.unscaledTime < _nextMissRetryAt) return null;
            _nextMissRetryAt = Time.unscaledTime + MissRetrySeconds;

            try
            {
                var tagged = GameObject.FindWithTag("Player");
                if (tagged != null) return _cached = tagged.transform;
            }
            catch (System.Exception)
            {
                // FindWithTag throws if the tag is not defined in this project.
            }

            if (PlayerFarming.Instance != null)
                return _cached = PlayerFarming.Instance.transform;

            var prisoner = Object.FindObjectOfType<PlayerPrisonerController>();
            if (prisoner != null) return _cached = prisoner.transform;

            return null;
        }

        /// <summary>The game's own authority for whether an interaction is in range.</summary>
        public static Interactor ResolveInteractor()
        {
            if (_cachedInteractor != null && _cachedInteractor.gameObject.activeInHierarchy)
                return _cachedInteractor;

            var player = Resolve();
            if (player == null) return null;

            _cachedInteractor = player.GetComponent<Interactor>() ??
                                player.GetComponentInParent<Interactor>() ??
                                player.GetComponentInChildren<Interactor>();
            return _cachedInteractor;
        }

        public static void Forget()
        {
            _cached = null;
            _cachedInteractor = null;
        }
    }
}
