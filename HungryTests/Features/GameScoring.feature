Feature: GameInfo Tests

@Scoring
Scenario: Every pill eaten increases the point value of the next pill.
	Given player1 joins
	And the game starts with 2 rows, 2 columns
	When player1 eats a pill
	Then player1's score is 1
	When player1 eats another pill
	Then player1's score is 3
	When player1 eats another pill
	Then player1's score is 6