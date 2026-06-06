using System.Collections.Generic;
using RagNextPlayer.Runtime;
using RagNextPlayer.Runtime.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace RagNextPlayer.Managers
{
    /// <summary>
    /// Implements IGameEventSink. Receives every executed command from ActionExecutor
    /// and routes its side-effects to the correct manager — exactly as MainPage.HandleCommandEffect
    /// did in the MAUI player, but decoupled from any MAUI types.
    /// </summary>
    public class CommandEffectRouter : MonoBehaviour, IGameEventSink
    {
        private void OnEnable()
        {
            // No OnRoomEntered subscription here — UIManager handles room rendering.
        }

        private void Start()
        {
            // No OnRoomEntered subscription here — UIManager handles room rendering.
        }

        private void OnDisable()
        {
            // Nothing to unsubscribe.
        }

        // CommandEffectRouter does NOT handle OnRoomEntered.
        // UIManager subscribes to GameManager.OnRoomEntered and calls RenderRoom directly.
        // Having both call RenderRoom causes double narrative entries.

        // ── IGameEventSink ────────────────────────────────────────────────────

        public void OnCommandExecuted(CommandData cmd, GameExecutionContext ctx)
        {
            // UI calls must happen on the main thread — in Unity single-threaded
            // model this is always safe unless called from a Task continuation.
            HandleCommandEffect(cmd, ctx);
        }

        public void OnConditionEvaluated(ConditionData cond, bool result, GameExecutionContext ctx)
        {
            // Log in debug builds; no direct UI effect
            Debug.Log($"[Condition] {cond.Type} → {result}");
        }

        // ── Command → UI Routing ──────────────────────────────────────────────

        private void HandleCommandEffect(CommandData cmd, GameExecutionContext ctx)
        {
            switch (cmd)
            {
                case DisplayTextCommandData c:
                    // ctx.Resolve includes FocusObject, room, and player context
                    UIManager.Instance?.AppendNarrativeText(ctx.Resolve(c.Text));
                    break;



                case MovePlayerToRoomCommandData c:
                    // State was already written to player.currentRoomId by ActionExecutor.
                    // Now trigger the animated room transition.
                    var roomId = ctx.GetVariable("player.currentRoomId")?.Value;
                    if (!string.IsNullOrEmpty(roomId))
                        GameManager.Instance?.MovePlayerToRoom(roomId);
                    break;

                case AddObjectToRoomCommandData:
                case RemoveObjectFromRoomCommandData:
                case OpenContainerCommandData:
                case CloseContainerCommandData:
                case ObjectMoveToInventoryCommandData:
                case ObjectMoveToCharacterCommandData:
                case ObjectMoveInsideObjectCommandData:
                    UIManager.Instance?.RefreshEntityLists();
                    break;

                case ObjectDisplayDescriptionCommandData:
                case PlayerDisplayDescriptionCommandData:
                case CharacterDisplayDescriptionCommandData:
                case RoomDisplayDescriptionCommandData:
                    {
                        var text = ctx.GetVariable("system.lastDisplayedText")?.Value;
                        if (!string.IsNullOrEmpty(text))
                        {
                            UIManager.Instance?.AppendNarrativeText(text);
                        }
                    }
                    break;

                case SetRoomExitCommandData:
                case DisableRoomExitCommandData:
                case LockRoomExitCommandData:
                case UnlockRoomExitCommandData:
                    UIManager.Instance?.RefreshExits();
                    break;

                case SetVariableCommandData c:
                    // If a script moved the player by writing to player.currentRoomId directly
                    if (string.Equals(c.Name, "player.currentRoomId", System.StringComparison.OrdinalIgnoreCase))
                    {
                        var targetId = ctx.GetVariable("player.currentRoomId")?.Value;
                        if (!string.IsNullOrEmpty(targetId))
                            GameManager.Instance?.MovePlayerToRoom(targetId);
                    }
                    break;

                case PlayerSetNameCommandData:
                    UIManager.Instance?.RefreshPlayerPanel();
                    break;

                case PlayerSetGenderCommandData:
                    UIManager.Instance?.RefreshPlayerPanel();
                    break;

                case PlayerSetDescriptionCommandData c:
                    UIManager.Instance?.AppendNarrativeText($"[Player] {ctx.Game.Player.Description}");
                    break;

                case PlayerSetPortraitMediaCommandData:
                    UIManager.Instance?.RefreshPlayerPortrait();
                    break;

                case DisplayMultimediaCommandData:
                    {
                        var mediaId  = ctx.GetVariable("media.lastDisplayedMediaId")?.Value;
                        if (!string.IsNullOrEmpty(mediaId))
                        {
                            var asset = ctx.Game.MediaAssets.Find(a => a.Id == mediaId);
                            var path = asset is not null ? asset.RelativePath : mediaId;
                            UIManager.Instance?.DisplaySceneImage(path);
                        }
                    }
                    break;

                case CharacterDisplayPortraitCommandData c:
                    {
                        var portId = ctx.GetVariable($"char.{ctx.Resolve(c.CharacterId)}.displayedPortraitId")?.Value;
                        if (!string.IsNullOrEmpty(portId))
                        {
                            var asset  = ctx.Game.MediaAssets.Find(a => a.Id == portId);
                            var path = asset is not null ? asset.RelativePath : portId;
                            UIManager.Instance?.DisplaySceneImage(path);
                        }
                    }
                    break;

                case PlaySoundEffectCommandData c:
                    {
                        var soundId = ctx.Resolve(c.SoundId);
                        AudioManager.Instance?.PlaySound(soundId, (float)(c.Volume / 100.0), c.Loop, (float)c.StartTime, (float)c.EndTime);
                    }
                    break;

                case PlayVideoCommandData c:
                    {
                        var videoId = ctx.Resolve(c.VideoId);
                        // Route play video event to UIManager if it has video player controls
                        UIManager.Instance?.PlaySceneVideo(videoId, (float)(c.Volume / 100.0), c.Loop, (float)c.StartTime, (float)c.EndTime);
                    }
                    break;

                case StopSoundEffectCommandData c:
                    {
                        if (c.StopAllLooping)
                        {
                            AudioManager.Instance?.StopAllLoopingSounds();
                        }
                        else
                        {
                            var soundId = ctx.Resolve(c.SoundId);
                            AudioManager.Instance?.StopSound(soundId);
                        }
                    }
                    break;

                case EndGameCommandData c:
                    UIManager.Instance?.ShowGameOverScreen(ctx.Resolve(c.FinalMessage));
                    break;

                case PromptPlayerInputCommandData c:
                    UIManager.Instance?.ShowPromptInputScreen(
                        ctx.Resolve(c.PromptName),
                        ctx.Resolve(c.PromptText),
                        c.InputType,
                        c.CustomOptions,
                        c.StoreVariableName
                    );
                    break;

                case StartDialogueCommandData c:
                    UIManager.Instance?.ShowDialogueScreen(c, ctx);
                    break;

                case AddCustomChoiceCommandData:
                case ClearCustomChoiceCommandData:
                case RemoveCustomChoiceCommandData:
                    break;
            }
        }
    }
}
