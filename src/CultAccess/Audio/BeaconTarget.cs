using UnityEngine;

namespace CultAccess.Audio
{
    /// <summary>One independently owned source for the shared audio beacon.</summary>
    internal sealed class BeaconTarget
    {
        private Transform _follow;
        private Vector3 _offset;
        private Vector3 _fixedPosition;
        private bool _hasFixedPosition;

        internal bool Active => _follow != null || _hasFixedPosition;

        internal bool SetFollow(Transform follow, Vector3 offset)
        {
            if (_follow == follow && !_hasFixedPosition && _offset == offset) return false;
            _follow = follow;
            _offset = offset;
            _hasFixedPosition = false;
            return true;
        }

        internal bool SetPosition(Vector3 position)
        {
            if (_follow == null && _hasFixedPosition && _fixedPosition == position) return false;
            _follow = null;
            _offset = Vector3.zero;
            _fixedPosition = position;
            _hasFixedPosition = true;
            return true;
        }

        internal bool Clear()
        {
            // Unity's destroyed-object equality reports a stale Transform as null. Use
            // reference identity here so that stale slots are still actually emptied.
            if (ReferenceEquals(_follow, null) && !_hasFixedPosition) return false;
            _follow = null;
            _offset = Vector3.zero;
            _hasFixedPosition = false;
            return true;
        }

        internal bool TryGetPosition(out Vector3 position, out bool followsTransform)
        {
            if (_follow != null)
            {
                position = _follow.position + _offset;
                followsTransform = true;
                return true;
            }

            if (_hasFixedPosition)
            {
                position = _fixedPosition;
                followsTransform = false;
                return true;
            }

            position = Vector3.zero;
            followsTransform = false;
            return false;
        }
    }
}
