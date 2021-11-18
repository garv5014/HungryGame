Feature: GameInfo Tests

@Scoring
Scenario: Every pill eaten increases the point value of the next pill.
	Given player1 joins
	And the game starts with 2 rows, 2 columns
	When player1 eats a pill
	Then player1's score is 1
	And their new location is (0,0)
	And they did eat a pill
	When player1 eats another pill
	Then player1's score is 3
	When player1 eats another pill
	Then player1's score is 6

@gameLogic
Scenario: Players can join after a game has started
	Given player1 joins
	And the game starts with 2 rows, 2 columns
	When player2 joins
	Then player2 gets a valid token
	And there are two players