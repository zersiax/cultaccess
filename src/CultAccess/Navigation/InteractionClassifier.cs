using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>The player's reason for finding an interaction, independent of availability.</summary>
    internal enum InteractionRole
    {
        None,
        Facility,
        Resource,
        Story,
        Travel,
        Other,
    }

    /// <summary>Bulk natural resources that can be represented by their nearest useful node.</summary>
    internal enum ResourceRole
    {
        None,
        Lumber,
        Stone,
        Food,
    }

    internal readonly struct InteractionClassification
    {
        public InteractionClassification(
            InteractionRole role,
            ResourceRole resource = ResourceRole.None,
            bool bulk = false,
            bool omitWhenPrimaryDisabled = false)
        {
            Role = role;
            Resource = resource;
            Bulk = bulk;
            OmitWhenPrimaryDisabled = omitWhenPrimaryDisabled;
        }

        public InteractionRole Role { get; }
        public ResourceRole Resource { get; }
        public bool Bulk { get; }

        /// <summary>
        /// Used only where a concrete subclass makes its primary flag authoritative for
        /// relevance, rather than merely for generic interaction availability.
        /// </summary>
        public bool OmitWhenPrimaryDisabled { get; }
    }

    /// <summary>
    /// Gives interactions stable semantic roles. These mappings intentionally use exact
    /// runtime types and owning structures, never guesses from a localized label.
    /// </summary>
    internal static class InteractionClassifier
    {
        private static readonly Dictionary<Type, MemberInfo> StructureMembers =
            new Dictionary<Type, MemberInfo>();

        private static readonly Dictionary<string, ResourceRole> BulkResources =
            new Dictionary<string, ResourceRole>(StringComparer.Ordinal)
            {
                { "Interaction_Woodcutting", ResourceRole.Lumber },
                { "Interaction_WoodcuttingRubble", ResourceRole.Lumber },
                { "Interaction_PlayerClearRubble", ResourceRole.Stone },
                { "Interaction_Berries", ResourceRole.Food },
            };

        private static readonly HashSet<string> StoryTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "FollowerRecruit",
                "Interaction_AwaitRecruit",
                "Interaction_BaalRescue",
                "Interaction_FakeHidingSpot",
                "Interaction_FirstFreeFollower",
                "Interaction_FireHealingRoomNPC",
                "Interaction_FishermanRatoo",
                "Interaction_FishermanWinter",
                "Interaction_FlockadeNPC",
                "Interaction_FollowerHappyNPC",
                "Interaction_FollowerInSpiderWeb",
                "Interaction_FollowerInSpiderWebDailyShop",
                "Interaction_FollowerSinNPC",
                "Interaction_FollowerSpawn",
                "Interaction_FollowerSpawnWarriorTrio",
                "Interaction_GhostChildrenNPC",
                "Interaction_GofernonRotburn",
                "Interaction_IceHealingRoomNPC",
                "Interaction_IcegoreCaveFootprints",
                "Interaction_IntroKneelAltar",
                "Interaction_Lore_Professor",
                "Interaction_LoreHaro",
                "Interaction_RatooRescue",
                "Interaction_Sherpa",
                "Interaction_SimpleConversation",
                "Interaction_Villager",
            };

        private static readonly HashSet<string> TravelTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "BaseGoopDoor",
                "Inteaction_DoorRoomDoor",
                "Interaction_BaseDoor",
                "Interaction_BaseDungeonDoor",
                "Interaction_BaseTeleporter",
                "Interaction_BiomeDoor",
                "Interaction_BossPortal",
                "Interaction_CultDoor",
                "Interaction_DungeonPortal",
                "Interaction_Elevator",
                "Interaction_GoToScene",
                "Interaction_HubBossDoor",
                "Interaction_LockedDoor",
                "Interaction_NextFloor",
                "Interaction_SoulDoor",
                "Interaction_Teleporter",
                "Interaction_TeleporterMap",
                "Interaction_TeleportHome",
                "Interaction_TempleBossDoor",
                "Interaction_WolfDoor",
            };

        private static readonly HashSet<string> FacilityTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Interaction_CollectedResources",
                "Interaction_CollectResourceChest",
                "Interaction_MissionShrine",
                "Interaction_PlacementRegion",
                "Interaction_PlayerBuild",
                "Interaction_PlayerBuildProject",
                "Interaction_Reindoctrinate",
                "Interaction_StructureMenu",
                // Sermon and ritual surfaces inside the Temple. They carry no owning
                // Structure reference, so the generic ownership path left them in Other and
                // the Facilities filter never listed them.
                "Interaction_TempleAltar",
                "Interaction_OutpostTemple",
            };

        public static InteractionClassification Classify(Interaction interaction)
        {
            if (interaction == null) return default;

            var typeName = interaction.GetType().Name;
            if (BulkResources.TryGetValue(typeName, out var resource))
                return new InteractionClassification(
                    InteractionRole.Resource, resource, bulk: true);

            if (StoryTypes.Contains(typeName))
            {
                // Interaction_FakeHidingSpot.Update assigns Interactable directly from its
                // private IsObjectiveActive result. This is positive quest-state evidence:
                // unlike an empty label, false here really means the spot is irrelevant.
                return new InteractionClassification(
                    InteractionRole.Story,
                    omitWhenPrimaryDisabled: typeName == "Interaction_FakeHidingSpot");
            }

            if (TravelTypes.Contains(typeName))
                return new InteractionClassification(InteractionRole.Travel);

            if (FacilityTypes.Contains(typeName) || OwningStructure(interaction) != null)
                return new InteractionClassification(InteractionRole.Facility);

            // Unknown subclasses remain visible in Everything. Adding a
            // new game interaction must never make it disappear because this map is stale.
            return new InteractionClassification(InteractionRole.Other);
        }

        /// <summary>
        /// Prefabs expose their owning structure in either of two authoritative ways. Some
        /// put the interaction beneath the Structure component; many others serialize a
        /// direct Structure field (Cooking Fire's Interaction_Cauldron is one example).
        /// </summary>
        internal static Structure OwningStructure(Interaction interaction)
        {
            if (interaction == null) return null;

            var parent = interaction.GetComponentInParent<Structure>();
            if (parent != null) return parent;

            // Build-site interactions reference a plot, which in turn owns the Structure.
            if (interaction is Interaction_PlayerBuild build)
                return build.buildSitePlot?.Structure;
            if (interaction is Interaction_PlayerBuildProject project)
                return project.buildSitePlot?.Structure;

            var type = interaction.GetType();
            if (!StructureMembers.TryGetValue(type, out var member))
            {
                member = FindStructureMember(type);
                StructureMembers[type] = member;
            }

            try
            {
                if (member is FieldInfo field)
                    return field.GetValue(interaction) as Structure;
                if (member is PropertyInfo property)
                    return property.GetValue(interaction, null) as Structure;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning(
                    $"Could not read owning structure from {type.Name}: {e.Message}");
            }

            return null;
        }

        private static MemberInfo FindStructureMember(Type type)
        {
            MemberInfo fallback = null;
            for (var current = type;
                 current != null && typeof(Interaction).IsAssignableFrom(current);
                 current = current.BaseType)
            {
                foreach (var field in current.GetFields(
                             BindingFlags.Instance | BindingFlags.Public |
                             BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!typeof(Structure).IsAssignableFrom(field.FieldType)) continue;
                    if (IsStructureMemberName(field.Name)) return field;
                    if (fallback == null) fallback = field;
                }
            }
            if (fallback != null) return fallback;

            for (var current = type;
                 current != null && typeof(Interaction).IsAssignableFrom(current);
                 current = current.BaseType)
            {
                foreach (var property in current.GetProperties(
                             BindingFlags.Instance | BindingFlags.Public |
                             BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!typeof(Structure).IsAssignableFrom(property.PropertyType) ||
                        property.GetIndexParameters().Length != 0)
                        continue;
                    if (IsStructureMemberName(property.Name)) return property;
                    if (fallback == null) fallback = property;
                }
            }
            return fallback;
        }

        private static bool IsStructureMemberName(string name) =>
            string.Equals(name?.Trim('_'), "structure", StringComparison.OrdinalIgnoreCase);

        public static string ResourceName(ResourceRole resource)
        {
            switch (resource)
            {
                case ResourceRole.Lumber: return "lumber resource";
                case ResourceRole.Stone: return "stone resource";
                case ResourceRole.Food: return "food resource";
                default: return "resource";
            }
        }
    }
}
