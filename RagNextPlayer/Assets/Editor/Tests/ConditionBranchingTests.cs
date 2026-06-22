using NUnit.Framework;
using RagNextPlayer.Runtime;
using RagNextPlayer.Runtime.Models;
using System.Collections.Generic;

namespace RagNextPlayer.Tests
{
    public class ConditionBranchingTests
    {
        private GameData _game;
        private GameExecutionContext _ctx;

        [SetUp]
        public void Setup()
        {
            _game = new GameData
            {
                Title = "Test Game",
                Variables = new List<GameVariableData>(),
                Rooms = new List<RoomData>(),
                Characters = new List<GameObjectData>(),
                Objects = new List<GameObjectData>(),
                Player = new PlayerData()
            };

            _ctx = new GameExecutionContext(_game);
        }

        [Test]
        public void Evaluate_VariableEqualsCondition_MatchesCorrectly()
        {
            _game.Variables.Add(new GameVariableData { Name = "quest_stage", Value = "active", Type = "string" });
            
            var condTrue = new VariableEqualsConditionData { Name = "quest_stage", Value = "active" };
            var condFalse = new VariableEqualsConditionData { Name = "quest_stage", Value = "completed" };

            Assert.IsTrue(ActionExecutor.EvaluateCondition(condTrue, _ctx));
            Assert.IsFalse(ActionExecutor.EvaluateCondition(condFalse, _ctx));
        }

        [Test]
        public void Evaluate_PlayerInRoomCondition_EvaluatesCorrectly()
        {
            var room = new RoomData { Id = "milestone_42" };
            _ctx.CurrentRoom = room;

            var condTrue = new PlayerInRoomConditionData { RoomId = "milestone_42" };
            var condFalse = new PlayerInRoomConditionData { RoomId = "milestone_43" };

            Assert.IsTrue(ActionExecutor.EvaluateCondition(condTrue, _ctx));
            Assert.IsFalse(ActionExecutor.EvaluateCondition(condFalse, _ctx));
        }

        [Test]
        public void Evaluate_ItemHeldByPlayerCondition_EvaluatesCorrectly()
        {
            _game.Player.Inventory.Add(new GameObjectData { Id = "key_gold" });

            var condTrue = new ItemHeldByPlayerConditionData { ItemId = "key_gold" };
            var condFalse = new ItemHeldByPlayerConditionData { ItemId = "key_silver" };

            Assert.IsTrue(ActionExecutor.EvaluateCondition(condTrue, _ctx));
            Assert.IsFalse(ActionExecutor.EvaluateCondition(condFalse, _ctx));
        }

        [Test]
        public void Evaluate_VariableComparisonCondition_MatchesCorrectly()
        {
            _game.Variables.Add(new GameVariableData { Name = "strength", Value = "15", Type = "int" });

            var checkTrue = new VariableComparisonConditionData { Name = "strength", Comparison = ">=", Value = "10" };
            var checkFalse = new VariableComparisonConditionData { Name = "strength", Comparison = "<", Value = "12" };

            Assert.IsTrue(ActionExecutor.EvaluateCondition(checkTrue, _ctx));
            Assert.IsFalse(ActionExecutor.EvaluateCondition(checkFalse, _ctx));
        }

        [Test]
        public void Evaluate_IsRoomExitLockedCondition_EvaluatesCorrectly()
        {
            var room = new RoomData { Id = "bridge" };
            room.LockedExits["West"] = true;
            _game.Rooms.Add(room);

            var checkTrue = new IsRoomExitLockedConditionData { RoomId = "bridge", Direction = "West" };
            var checkFalse = new IsRoomExitLockedConditionData { RoomId = "bridge", Direction = "East" };

            Assert.IsTrue(ActionExecutor.EvaluateCondition(checkTrue, _ctx));
            Assert.IsFalse(ActionExecutor.EvaluateCondition(checkFalse, _ctx));
        }
    }
}
