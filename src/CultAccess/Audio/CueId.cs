namespace CultAccess.Audio
{
    /// <summary>
    /// Every sound the mod can make, as one identity space.
    ///
    /// One enum rather than several because the promise to the player is that *every* cue is
    /// individually togglable and has its own volume. That promise is only checkable if there
    /// is a single list to check it against: settings, the configuration menu, and the learn
    /// sounds walkthrough are all generated from this enum, so a cue cannot be added without
    /// becoming configurable and teachable at the same time.
    /// </summary>
    internal enum CueId
    {
        /// <summary>Navigation and enemy-lock beacon ping.</summary>
        Beacon,

        WallNear,
        WallBlocked,
        DodgeDirection,
        DodgeBlocked,
        MeleeThreat,
        ProjectileThreat,
        AreaThreat,
        StaticTrap,
        DodgeAvoidedHit,

        /// <summary>Sustained tone held open for the whole cooking success window.</summary>
        TimingWindow,

        /// <summary>Short chirp at the edge of a timing window, when the tone is off.</summary>
        TimingChirp,

        AmbientWall,
        AmbientItem,
        AmbientInteractable,
        AmbientNpc,
        AmbientEnemy,
        AmbientProjectile,
    }

    /// <summary>Which part of the configuration menu a cue belongs under.</summary>
    internal enum CueGroup
    {
        Wayfinding,
        Combat,
        Minigames,
        Ambient,
    }

    internal static class Cues
    {
        /// <summary>
        /// Declaration order, which is also the order the menu and the learn-sounds
        /// walkthrough present them in.
        /// </summary>
        internal static readonly CueId[] All =
        {
            CueId.Beacon,
            CueId.WallNear,
            CueId.WallBlocked,
            CueId.DodgeDirection,
            CueId.DodgeBlocked,
            CueId.MeleeThreat,
            CueId.ProjectileThreat,
            CueId.AreaThreat,
            CueId.StaticTrap,
            CueId.DodgeAvoidedHit,
            CueId.TimingWindow,
            CueId.TimingChirp,
            CueId.AmbientWall,
            CueId.AmbientItem,
            CueId.AmbientInteractable,
            CueId.AmbientNpc,
            CueId.AmbientEnemy,
            CueId.AmbientProjectile,
        };

        /// <summary>
        /// Check that <see cref="All"/> really lists every cue, and report by name if it does
        /// not.
        ///
        /// The list is written out by hand because its order is the order the menu and the
        /// walkthrough present, which no reflection over the enum can be relied on to give.
        /// The cost of that choice is that a cue added to the enum and forgotten here would
        /// get no setting, no menu row and no lesson, and would simply never sound — a silent
        /// failure, and the one kind this project treats as unacceptable. So it is checked at
        /// startup instead of trusted.
        /// </summary>
        internal static bool Validate(System.Action<string> report)
        {
            var complete = true;

            foreach (CueId cue in System.Enum.GetValues(typeof(CueId)))
            {
                var listed = false;
                foreach (var known in All)
                    if (known == cue)
                    {
                        listed = true;
                        break;
                    }

                if (listed) continue;

                complete = false;
                report($"Cue {cue} is missing from Cues.All, so it has no setting, no menu " +
                       "row and no entry in Learn sounds, and will never play.");
            }

            return complete;
        }

        internal static CueGroup Group(CueId cue)
        {
            switch (cue)
            {
                case CueId.Beacon:
                    return CueGroup.Wayfinding;
                case CueId.TimingWindow:
                case CueId.TimingChirp:
                    return CueGroup.Minigames;
                case CueId.AmbientWall:
                case CueId.AmbientItem:
                case CueId.AmbientInteractable:
                case CueId.AmbientNpc:
                case CueId.AmbientEnemy:
                case CueId.AmbientProjectile:
                    return CueGroup.Ambient;
                default:
                    return CueGroup.Combat;
            }
        }

        internal static bool IsAmbient(CueId cue) => Group(cue) == CueGroup.Ambient;

        /// <summary>
        /// The localisation key stem for a cue's name and description. Descriptive, never
        /// numbered, so a translator can work through the catalogue without the game.
        /// </summary>
        internal static string Key(CueId cue)
        {
            switch (cue)
            {
                case CueId.Beacon: return "cue.beacon";
                case CueId.WallNear: return "cue.wall_near";
                case CueId.WallBlocked: return "cue.wall_blocked";
                case CueId.DodgeDirection: return "cue.dodge_direction";
                case CueId.DodgeBlocked: return "cue.dodge_blocked";
                case CueId.MeleeThreat: return "cue.melee_threat";
                case CueId.ProjectileThreat: return "cue.projectile_threat";
                case CueId.AreaThreat: return "cue.area_threat";
                case CueId.StaticTrap: return "cue.static_trap";
                case CueId.DodgeAvoidedHit: return "cue.dodge_avoided_hit";
                case CueId.TimingWindow: return "cue.timing_window";
                case CueId.TimingChirp: return "cue.timing_chirp";
                case CueId.AmbientWall: return "cue.ambient_wall";
                case CueId.AmbientItem: return "cue.ambient_item";
                case CueId.AmbientInteractable: return "cue.ambient_interactable";
                case CueId.AmbientNpc: return "cue.ambient_npc";
                case CueId.AmbientEnemy: return "cue.ambient_enemy";
                case CueId.AmbientProjectile: return "cue.ambient_projectile";
                default: return "cue.unknown";
            }
        }

        /// <summary>
        /// The config-file key for a cue, kept stable and separate from the display name so
        /// that renaming a cue in the interface never silently resets a player's settings.
        /// </summary>
        internal static string ConfigKey(CueId cue) => cue.ToString();

        /// <summary>
        /// The file stem a player uses to replace this cue, under <c>sounds/</c> beside the
        /// plugin. Written in words rather than derived from the enum, because this name is
        /// typed by a person reading a folder listing with a screen reader; "dodge-avoided-hit"
        /// is legible read aloud and "DodgeAvoidedHit" is not.
        /// </summary>
        internal static string SoundFileName(CueId cue)
        {
            switch (cue)
            {
                case CueId.Beacon: return "beacon";
                case CueId.WallNear: return "wall-ahead";
                case CueId.WallBlocked: return "wall-contact";
                case CueId.DodgeDirection: return "dodge-direction";
                case CueId.DodgeBlocked: return "dodge-into-wall";
                case CueId.MeleeThreat: return "melee-windup";
                case CueId.ProjectileThreat: return "incoming-shot";
                case CueId.AreaThreat: return "danger-area";
                case CueId.StaticTrap: return "static-trap";
                case CueId.DodgeAvoidedHit: return "dodge-avoided-hit";
                case CueId.TimingWindow: return "timing-window";
                case CueId.TimingChirp: return "timing-edge";

                // The wall ring has one file per direction rather than one for the category,
                // so a replacement is played exactly as recorded instead of being pitch
                // shifted eight ways. See WallDirection.
                case CueId.AmbientWall: return "wall-north";

                case CueId.AmbientItem: return "near-item";
                case CueId.AmbientInteractable: return "near-interactable";
                case CueId.AmbientNpc: return "near-character";
                case CueId.AmbientEnemy: return "near-enemy";
                case CueId.AmbientProjectile: return "near-projectile";
                default: return "unknown";
            }
        }
    }
}
