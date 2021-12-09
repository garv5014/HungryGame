Feature: DeathMatch

How do we handle a death match?

@tag1
Scenario: 2 player death match on 2x2 board
	Given the game state is Joining
	Given p1 joins
	And p2 joins
	And the game starts with 2 rows, 2 columns
	Then the game state is Eating
	When p1 moves Left
	Then p1's score is 1
	And p1's location is (0,0)
	When p2 moves Left
	Then p2's score is 2
	And p2's location is (1,0)
	And the game state is Battle
	When p2 moves Up
	Then p1 is removed from the board
	And p2's score is 1
	And p2 is declared winner
	And the game state is GameOver

