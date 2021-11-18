using FluentAssertions;
using Gherkin;
using HungryHippos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using TechTalk.SpecFlow;

namespace HungryTests.StepDefinitions
{
    [Binding]
    public class GameInfoTestsStepDefinitions
    {
        private readonly ScenarioContext context;
        private const string SECRET_CODE = nameof(SECRET_CODE);
        private static int lastRandom = 0;
        private readonly Queue<Direction> moves = new(new[]
        {
            Direction.Left,
            Direction.Down,
            Direction.Right
        });

        public GameInfoTestsStepDefinitions(ScenarioContext context)
        {
            this.context = context;
        }

        private GameInfo getGame()
        {
            if (context.TryGetValue(out GameInfo game) is false)
            {
                var configMock = new Mock<IConfiguration>();
                configMock.Setup(m => m["SECRET_CODE"]).Returns(SECRET_CODE);
                var loggerMock = new Mock<ILogger<GameInfo>>();
                var randomMock = new Mock<IRandomService>();
                randomMock.Setup(m => m.Next(It.IsAny<int>())).Returns(() => 
                {
                    lastRandom++;
                    if(lastRandom > 2)
                        lastRandom = 0;
                    return lastRandom;
                });
                game = new GameInfo(configMock.Object, loggerMock.Object, randomMock.Object);
                context.Set(game);
            }
            return game;
        }

        [Given(@"(.*) joins")]
        [When(@"(.*) joins")]
        public void GivenPlayerJoins(string playerName)
        {
            var game = getGame();
            var token = game.JoinPlayer(playerName);
            context.Add(playerName, token);
        }

        [Given(@"the game starts with (.*) rows, (.*) columns")]
        public void GivenTheGameStarts(int numRows, int numColumns)
        {
            var game = getGame();
            game.StartGame(numRows, numColumns, SECRET_CODE);
        }

        [When(@"(.*) eats a pill")]
        [When(@"(.*) eats another pill")]
        public void WhenPlayerEatsAPill(string playerName)
        {
            var game = getGame();
            var token = context.Get<string>(playerName);
            MoveResult result = game.Move(token, moves.Dequeue());
            context.Set(result);
        }

        [Then(@"(.*)'s score is (.*)")]
        public void ThenPlayersScoreIs(string playerName, int score)
        {
            var game = getGame();
            var players = game.GetPlayers();
            players.First(p => p.Name == playerName).Score.Should().Be(score);
        }

        [Then(@"their new location is \((.*),(.*)\)")]
        public void ThenPlayersLocationIs(int row, int col)
        {
            var moveResult = context.Get<MoveResult>();
            moveResult.NewLocation.Should().Be(new Location(row, col));
        }

        [Then(@"they did eat a pill")]
        public void ThenTheyDidEatAPill()
        {
            var moveResult = context.Get<MoveResult>();
            moveResult.AteAPill.Should().BeTrue();
        }

        [Then(@"(.*) gets a valid token")]
        public void Thenplayergetsavalidtoken(string playerName)
        {
            var token = context.Get<string>(playerName);
            token.Should().NotBeNullOrEmpty();
        }

        [Then(@"there are two players")]
        public void Giventherearetwoplayers()
        {
            var game = getGame();
            game.GetPlayers().Count().Should().Be(2);
        }

    }
}
