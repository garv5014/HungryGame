Feature: DeathMatch

A short summary of the feature

/*
//create a 2x2 game
//add two players
//p1 eats a pill
//p1's score is now 1
//p2 eats a pill
//p2's score is now 2
//p1 attacks p2
//p1 should be removed from board
//p2's score is now 1 (reduced by the attacking player's health)
//p2 wins because he is the last remaining contestant and there are no more pills
*/
@tag1
Scenario: 2 player death match on 2x2 board
	Given p1 joins
	And p2 joins
	And the game starts with 2 rows, 2 columns
	When p1 eats a pill
	Then p1's score is 1
	When p2 eats a pill
	Then p2's score is 2
	When p1 attacks p2
	Then p1 is removed from the board
	And p2's score is 1
	And p2 is declared winner

