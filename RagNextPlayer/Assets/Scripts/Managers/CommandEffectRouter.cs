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
                    {
                        // State was already written to player.currentRoomId by ActionExecutor.
                        // Now trigger the animated room transition.
                        var roomId = ctx.GetVariable("player.currentRoomId")?.Value;
                        if (!string.IsNullOrEmpty(roomId))
                            GameManager.Instance?.MovePlayerToRoom(roomId);
                    }
                    break;

                case AddObjectToRoomCommandData:
                case RemoveObjectFromRoomCommandData:
                case OpenContainerCommandData:
                case CloseContainerCommandData:
                case ObjectMoveToInventoryCommandData:
                case ObjectMoveToCharacterCommandData:
                case ObjectMoveInsideObjectCommandData:
                case WearItemCommandData:
                case RemoveItemCommandData:
                case CharacterMoveToRoomCommandData:
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
                    UIManager.Instance?.RefreshPlayerPanel();
                    break;

                case EvaluateFormulaCommandData c:
                    if (string.Equals(c.Name, "player.currentRoomId", System.StringComparison.OrdinalIgnoreCase))
                    {
                        var targetId = ctx.GetVariable("player.currentRoomId")?.Value;
                        if (!string.IsNullOrEmpty(targetId))
                            GameManager.Instance?.MovePlayerToRoom(targetId);
                    }
                    UIManager.Instance?.RefreshPlayerPanel();
                    break;

                case VariableIncrementCommandData:
                case VariableDecrementCommandData:
                case VariableSetToVariableCommandData:
                case SetNumericRandomlyCommandData:
                case SetArrayElementCommandData:
                case AddArrayRowCommandData:
                case RemoveArrayRowCommandData:
                case AppendTextCommandData:
                case AppendLineCommandData:
                    UIManager.Instance?.RefreshPlayerPanel();
                    break;

                case PlayerSetNameCommandData:
                case PlayerSetGenderCommandData:
                case SetPlayerAttributeCommandData:
                    UIManager.Instance?.RefreshPlayerPanel();
                    break;

                case PlayerSetDescriptionCommandData c:
                    UIManager.Instance?.AppendNarrativeText($"[Player] {ctx.Game.Player.Description}");
                    break;

                case PlayerSetPortraitMediaCommandData:
                    UIManager.Instance?.RefreshPlayerPortrait();
                    break;

                case CharacterSetPortraitMediaCommandData:
                    UIManager.Instance?.RefreshEntityLists();
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
                        var portId = ctx.GetVariable($"char.{ResolveCharacterId(c.CharacterId, ctx)}.displayedPortraitId")?.Value;
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
                        var path = videoId;
                        var asset = ctx.Game.MediaAssets.Find(a => a.Id == videoId);
                        if (asset != null) path = asset.RelativePath;
                        UIManager.Instance?.PlaySceneVideo(path, (float)(c.Volume / 100.0), c.Loop, (float)c.StartTime, (float)c.EndTime);
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

                case SetBackgroundMusicCommandData c:
                    {
                        var musicId = ctx.Resolve(c.MusicFile);
                        AudioManager.Instance?.PlayMusic(musicId);
                    }
                    break;

                case StopBackgroundMusicCommandData:
                    AudioManager.Instance?.StopMusic();
                    break;

                case EndGameCommandData c:
                    GameManager.Instance?.EndGame();
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

                case ShowStatusElementCommandData:
                case HideStatusElementCommandData:
                case SetStatusElementTextCommandData:
                case SetStatusElementImageCommandData:
                    UIManager.Instance?.RefreshPlayerPanel();
                    break;

                case AddCustomChoiceCommandData:
                case ClearCustomChoiceCommandData:
                case RemoveCustomChoiceCommandData:
                    break;
                case RoomDisplayPictureCommandData cDispPicture:
                    {
                        var displayRoomId = ResolveRoomId(cDispPicture.RoomId, ctx);
                        var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, displayRoomId, System.StringComparison.OrdinalIgnoreCase));
                        if (room != null && !string.IsNullOrEmpty(room.PortraitImagePath))
                        {
                            UIManager.Instance?.DisplaySceneImage(room.PortraitImagePath);
                        }
                    }
                    break;

                case PlayerMoveToCharacterCommandData:
                case PlayerMoveToObjectCommandData:
                    {
                        var targetId = ctx.GetVariable("player.currentRoomId")?.Value;
                        if (!string.IsNullOrEmpty(targetId))
                            GameManager.Instance?.MovePlayerToRoom(targetId);
                    }
                    break;

                case CharacterMoveInventoryToPlayerCommandData:
                case PlayerMoveInventoryToCharacterCommandData:
                case PlayerMoveInventoryToRoomCommandData:
                case RoomMoveItemsToPlayerCommandData:
                    UIManager.Instance?.RefreshEntityLists();
                    UIManager.Instance?.RefreshPlayerPanel();
                    break;

                case CharacterMoveToObjectCommandData:
                case CharacterSetDescriptionCommandData:
                case CharacterSetDisplayNameCommandData:
                case CharacterSetGenderCommandData:
                    UIManager.Instance?.RefreshEntityLists();
                    break;

                case RoomSetDescriptionCommandData cDesc:
                    {
                        var targetRoomId = ResolveRoomId(cDesc.RoomId, ctx);
                        var playerRoomId = ctx.GetVariable("player.currentRoomId")?.Value;
                        if (string.Equals(targetRoomId, playerRoomId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, targetRoomId, System.StringComparison.OrdinalIgnoreCase));
                            if (room != null)
                            {
                                UIManager.Instance?.RenderRoom(room);
                            }
                        }
                    }
                    break;

                case RoomSetPictureCommandData cPic:
                    {
                        var targetPicRoomId = ResolveRoomId(cPic.RoomId, ctx);
                        var playerRoomId = ctx.GetVariable("player.currentRoomId")?.Value;
                        if (string.Equals(targetPicRoomId, playerRoomId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, targetPicRoomId, System.StringComparison.OrdinalIgnoreCase));
                            if (room != null)
                            {
                                UIManager.Instance?.RenderRoom(room);
                            }
                        }
                    }
                    break;

                case SetStatusBarVisibleCommandData:
                    UIManager.Instance?.RefreshPlayerPanel();
                    break;

            }
        }

        private string ResolveRoomId(string input, GameExecutionContext ctx)
        {
            var resolved = ctx.Resolve(input);
            if (System.Guid.TryParse(resolved, out _)) return resolved;
            if (string.IsNullOrEmpty(resolved)) return resolved;
            var match = ctx.Game.Rooms.Find(r => 
                string.Equals(r.Name, resolved, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Name.Replace(" ", ""), resolved, System.StringComparison.OrdinalIgnoreCase));
            return match?.Id ?? resolved;
        }

        private string ResolveCharacterId(string input, GameExecutionContext ctx)
        {
            var resolved = ctx.Resolve(input);
            if (System.Guid.TryParse(resolved, out _)) return resolved;
            if (string.IsNullOrEmpty(resolved)) return resolved;
            var match = ctx.Game.Characters.Find(c => 
                string.Equals(c.Name, resolved, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Name.Replace(" ", ""), resolved, System.StringComparison.OrdinalIgnoreCase));
            return match?.Id ?? resolved;
        }
    }
}
