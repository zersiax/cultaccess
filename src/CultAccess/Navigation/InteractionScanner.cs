using System.Collections.Generic;
using CultAccess.Util;
using I2.Loc;
using MMRoomGeneration;
using MMTools;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>Scans prompt-driven interactions and automatic dungeon door triggers.</summary>
    internal static class InteractionScanner
    {
        public static void Add(Vector3 origin, float radiusSquared, List<PointOfInterest> found)
        {
            AddDoors(origin, radiusSquared, found);
            AddBuildingEntrances(origin, radiusSquared, found);
            AddInteractions(origin, radiusSquared, found);
        }

        /// <summary>
        /// Enterable buildings are a second scan source, like <c>Door.Doors</c>, because their
        /// doorways are trigger volumes rather than interactions. Without this pass a Temple,
        /// Blacksmith or shop is completely absent from the target list: nothing on the outside
        /// of one derives from <see cref="Interaction"/>.
        /// </summary>
        private static void AddBuildingEntrances(
            Vector3 origin,
            float radiusSquared,
            List<PointOfInterest> found)
        {
            foreach (var entrance in Object.FindObjectsOfType<EnterBuilding>())
            {
                if (!BuildingEntrance.ShouldInclude(entrance)) continue;

                var aim = BuildingEntrance.Aim(entrance);
                if ((aim - origin).sqrMagnitude > radiusSquared) continue;

                var name = BuildingEntrance.Name(entrance);
                var point = new PointOfInterest
                {
                    Transform = entrance.transform,
                    Name = name,
                    Kind = PoiKind.Interactable,
                    InteractionRole = InteractionRole.Travel,
                    BuildingEntranceTrigger = entrance,
                };
                found.Add(point);
                Diagnostics.NavigationDiagnostics.LogBuildingEntranceScan(entrance, name, aim);
            }
        }

        private static void AddInteractions(
            Vector3 origin,
            float radiusSquared,
            List<PointOfInterest> found)
        {
            var deferredByAltar = DeferredAltarConversations();

            // FindObjectsOfType includes disabled components on active objects. We normally
            // trust Interaction.interactions, which is maintained by OnEnable/OnDisable, but
            // retain unspoken SimpleConversation components as named, explicitly unavailable
            // landmarks: some story scripts enable them only when their scene is ready.
            foreach (var interaction in Object.FindObjectsOfType<Interaction>())
            {
                if (interaction == null) continue;

                var conversation = interaction as Interaction_SimpleConversation;
                var liveRegistryEntry = interaction.enabled &&
                                        Interaction.interactions != null &&
                                        Interaction.interactions.Contains(interaction);
                var dormantConversation = conversation != null && !conversation.Spoken;

                if (!liveRegistryEntry && !dormantConversation) continue;

                // Label getters frequently dereference their owning structure. Placement
                // previews and other unregistered components have no fully assigned brain,
                // so applying the game's own registry gate before reading labels avoids
                // invoking getters that cannot be valid yet. Dormant story conversations
                // remain the one deliberate exception, as documented above.
                var state = InteractionAvailability.Inspect(interaction);
                var classification = InteractionClassifier.Classify(interaction);
                var basePassage = interaction as BaseGoopDoor;
                var dungeonDoor = interaction as Interaction_BaseDungeonDoor;
                var dungeonDoorState = dungeonDoor == null
                    ? null
                    : DungeonDoorPassage.Inspect(dungeonDoor, state);
                var weaponPodium = interaction as Interaction_WeaponSelectionPodium;
                var followerInteraction = interaction as interaction_FollowerInteraction;
                if (basePassage != null && !BasePassage.ShouldInclude(basePassage))
                {
                    Diagnostics.NavigationDiagnostics.LogInteractionScan(
                        interaction, state, classification, null, included: false,
                        "entrance not revealed");
                    continue;
                }
                if (conversation != null && deferredByAltar.Contains(conversation) && !state.Available)
                {
                    Diagnostics.NavigationDiagnostics.LogInteractionScan(
                        interaction, state, classification, null, included: false,
                        "deferred by kneel altar");
                    continue;
                }
                if (classification.OmitWhenPrimaryDisabled && !state.Primary)
                {
                    Diagnostics.NavigationDiagnostics.LogInteractionScan(
                        interaction, state, classification, null, included: false,
                        "quest objective inactive");
                    continue;
                }

                var transform = interaction.transform;
                var returningToBase = false;
                var aimPosition = basePassage == null
                    ? dungeonDoor == null
                        ? transform.position + interaction.ActivatorOffset
                        : DungeonDoorPassage.Aim(dungeonDoor, dungeonDoorState)
                    : BasePassage.Aim(basePassage, origin, out returningToBase);
                if ((aimPosition - origin).sqrMagnitude > radiusSquared) continue;

                var name = InteractionName(interaction, state, classification);
                if (name == null)
                {
                    Diagnostics.NavigationDiagnostics.LogInteractionScan(
                        interaction, state, classification, null, included: false,
                        "no usable name");
                    continue;
                }

                var point = new PointOfInterest
                {
                    Transform = transform,
                    Name = name,
                    // A follower who owns a speak interaction is still a follower. Without
                    // this the Followers filter could never match anything in the base,
                    // because every follower there carries this component and the
                    // interaction scan reaches them first.
                    Kind = followerInteraction != null ? PoiKind.Follower : PoiKind.Interactable,
                    Interaction = interaction,
                    InteractionRole = classification.Role,
                    ResourceRole = classification.Resource,
                    IsBulkResource = classification.Bulk,
                    BasePassageGate = basePassage,
                    DungeonDoor = dungeonDoor,
                    WeaponPodium = weaponPodium,
                    AimPositionOverride = basePassage == null ? (Vector3?)null : aimPosition,
                    IsReturnPassage = basePassage != null && returningToBase,
                };
                found.Add(point);
                if (basePassage != null)
                    Diagnostics.NavigationDiagnostics.LogBasePassageScan(point, state);
                else if (dungeonDoor != null)
                    Diagnostics.NavigationDiagnostics.LogDungeonDoorScan(point, state);
                else if (weaponPodium != null)
                    Diagnostics.NavigationDiagnostics.LogWeaponPodiumScan(point, state);
                else
                    Diagnostics.NavigationDiagnostics.LogInteractionScan(
                        interaction, state, classification, name, included: true, null);
            }
        }

        private static void AddDoors(Vector3 origin, float radiusSquared, List<PointOfInterest> found)
        {
            if (Door.Doors == null) return;

            var seen = new HashSet<Door>();
            foreach (var door in Door.Doors)
            {
                if (door == null || !seen.Add(door)) continue;
                if (!door.gameObject.activeInHierarchy) continue;
                if (door.ConnectionType == GenerateRoom.ConnectionTypes.False) continue;
                if ((door.transform.position - origin).sqrMagnitude > radiusSquared) continue;

                var name = DoorName(door);
                found.Add(new PointOfInterest
                {
                    Transform = door.transform,
                    Name = name,
                    Kind = PoiKind.Exit,
                    Door = door,
                });
                Diagnostics.NavigationDiagnostics.LogDoorScan(door, name);
            }
        }

        private static HashSet<Interaction_SimpleConversation> DeferredAltarConversations()
        {
            var deferred = new HashSet<Interaction_SimpleConversation>();
            foreach (var altar in Object.FindObjectsOfType<Interaction_IntroKneelAltar>())
            {
                if (altar != null && altar.Conversation != null)
                    deferred.Add(altar.Conversation);
            }
            return deferred;
        }

        private static string InteractionName(
            Interaction interaction,
            InteractionAvailability state,
            InteractionClassification classification)
        {
            if (interaction is BaseGoopDoor passage)
                return BasePassage.Name(passage);

            if (interaction is Interaction_BaseDungeonDoor dungeonDoor)
                return DungeonDoorPassage.Name(dungeonDoor, state);

            if (interaction is Interaction_WeaponSelectionPodium weaponPodium)
                return WeaponPodiumTarget.Name(weaponPodium, state);

            if (interaction is Interaction_HeartPickupBase heartPickup)
                return HeartPickupTarget.Name(heartPickup);

            // Before the generic label path: a follower's label is blank at scan range, and
            // their own name is both stable and what the player actually needs to hear.
            if (interaction is interaction_FollowerInteraction followerInteraction)
            {
                var followerName = FollowerTarget.Name(followerInteraction);
                if (followerName != null) return followerName;
            }

            var conversation = interaction as Interaction_SimpleConversation;
            if (conversation != null)
            {
                var speaker = ConversationSpeakerName(conversation);
                if (speaker != null) return $"{speaker} conversation";
                return RichText.HasSemanticText(state.FirstLabel)
                    ? state.FirstLabel
                    : "story conversation";
            }

            var actionName = state.FirstLabel;
            if (!RichText.HasSemanticText(actionName))
            {
                // Keep the established stable names for temporarily label-less natural
                // resource nodes; their component class names are implementation details.
                if (classification.Bulk)
                    return InteractionClassifier.ResourceName(classification.Resource);
                actionName = FallbackInteractionName(interaction);
            }

            // Interactions are action-first in the game's UI ("Cook", "Build", ...),
            // but that is ambiguous in a target list containing several structures. Keep
            // the live action label while adding the owning building's localized name.
            if (classification.Role == InteractionRole.Facility)
            {
                var structure = InteractionClassifier.OwningStructure(interaction);
                if (structure != null)
                {
                    var isBuildAction = interaction is Interaction_PlayerBuild ||
                                        interaction is Interaction_PlayerBuildProject;
                    var type = StructureTypeForName(structure, isBuildAction);
                    var structureName = Building.PlacementDescriber.Name(type);

                    if (string.IsNullOrEmpty(actionName)) return structureName;
                    if (actionName.IndexOf(
                            structureName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return actionName;

                    return isBuildAction
                        ? $"{actionName} {structureName}"
                        : $"{structureName}, {actionName}";
                }
            }

            return RichText.HasSemanticText(actionName) ? actionName : null;
        }

        private static StructureBrain.TYPES StructureTypeForName(
            Structure structure, bool isBuildAction)
        {
            var data = structure.Structure_Info;
            if (isBuildAction && data != null &&
                data.ToBuildType != StructureBrain.TYPES.NONE)
                return data.ToBuildType;

            return data == null ? structure.Type : data.Type;
        }

        private static string FallbackInteractionName(Interaction interaction)
        {
            var typeName = interaction.GetType().Name;
            if (typeName.StartsWith("Interaction_", System.StringComparison.Ordinal))
                typeName = typeName.Substring("Interaction_".Length);
            else if (typeName.StartsWith("Inteaction_", System.StringComparison.Ordinal))
                typeName = typeName.Substring("Inteaction_".Length); // the game's own typo
            else if (typeName == "Interaction")
                return null;

            var humanised = RichText.Humanise(typeName);
            return humanised.Length == 0 ? null : humanised;
        }

        private static string ConversationSpeakerName(Interaction_SimpleConversation conversation)
        {
            var name = LocalisedName(conversation.StoryCharacterName);
            if (name != null) return name;

            if (conversation.Entries == null) return null;
            foreach (ConversationEntry entry in conversation.Entries)
            {
                if (entry == null || entry.SpeakerIsPlayer) continue;

                name = LocalisedName(entry.CharacterName);
                if (name != null) return name;

                if (entry.Speaker != null)
                {
                    name = WorldScanner.CharacterName(entry.Speaker.transform);
                    if (name != null) return name;
                }
            }
            return null;
        }

        private static string LocalisedName(string termOrName)
        {
            var raw = RichText.Clean(termOrName);
            if (raw.Length == 0 || raw == "-") return null;

            try
            {
                var translated = LocalizationManager.GetTranslation(termOrName);
                if (RichText.IsUsableLocalization(translated, termOrName))
                    return RichText.Clean(translated);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Conversation speaker localisation failed: {e.Message}");
            }
            var separator = raw.LastIndexOf('/');
            return separator >= 0
                ? RichText.Humanise(raw.Substring(separator + 1))
                : raw;
        }

        private static string DoorName(Door door)
        {
            var direction = door.direction.ToString().ToLowerInvariant();
            return door.ConnectionType == GenerateRoom.ConnectionTypes.Entrance
                ? $"{direction} entrance"
                : $"{direction} exit";
        }
    }
}
