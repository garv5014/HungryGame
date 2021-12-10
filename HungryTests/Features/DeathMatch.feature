Feature: DeathMatch

How do we handle a death match?

@tag1
Scenario: 2 player death match on 2x2 board
	Given the game state is Joining
	Given p1 joins
	And p2 joins
	And the game starts with 2 rows, 2 columns
	Then the game state is Eating
	When p1 moves Left and eats a pill
	Then p1's score is 1
	And p1's location is (0,0)
	When p2 moves Left and eats a pill
	Then p2's score is 2
	And p2's location is (1,0)
	And the game state is Battle
	When p2 moves Up and attacks
	Then p1 is removed from the board
	And p2's score is 1
	And p2 is declared winner
	And the game state is GameOver

Scenario: 2 player death match on 3x3 board
	Given the game state is Joining
	Given p1 joins
	And p2 joins
	And the game starts with 3 rows, 3 columns
	Then the game state is Eating
	When p1 moves Left and eats a pill
	Then p1's score is 1
	And p1's location is (0,0)
	When p2 moves Left and eats a pill
	Then p2's score is 2
	And p2's location is (1,0)
	And the board looks like
	| Board State |
	| 1_·         |
	| 2_·         |
	| ···         |
	When p2 moves Down and eats a pill
	Then p2's score is 5
	And p2's location is (2,0)
	And the board looks like
	| Board State |
	| 1_·         |
	| __·         |
	| 2··         |
	When p2 moves Right and eats a pill
	Then p2's score is 9
	And p2's location is (2,1)
	And the board looks like
	| Board State |
	| 1_·         |
	| __·         |
	| _2·         |
	When p2 moves Right and eats a pill
	Then p2's score is 14
	And p2's location is (2,2)
	And the board looks like
	| Board State |
	| 1_·         |
	| __·         |
	| __2         |
	When p1 moves Right and does nothing
	Then p1's score is 1
	And p1's location is (0,1)
	And the board looks like
	| Board State |
	| _1·         |
	| __·         |
	| __2         |
	When p1 moves Right and eats a pill
	Then p1's score is 7
	And p1's location is (0,2)
	And the board looks like
	| Board State |
	| __1         |
	| __·         |
	| __2         |
	When p2 moves Up and eats a pill
	Then p1's score is 7
	And p2's score is 21
	And p2's location is (1,2)
	And the board looks like
	| Board State |
	| __1         |
	| __2         |
	| ___         |
	And the game state is Battle
	When p2 moves Up and attacks
	Then p1 is removed from the board
	And p2's score is 14
	And p2's location is (1,2)
	And the board looks like
	| Board State |
	| __·         |
	| __2         |
	| ___         |
	When p2 moves Up and eats a pill
	Then p2's score is 18
	And p2's location is (0,2)
	And the board looks like
	| Board State |
	| __2         |
	| ___         |
	| ___         |
	And p2 is declared winner
	And the game state is GameOver
	

