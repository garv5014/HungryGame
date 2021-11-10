#Improvements
- Increase point value of pills as remaining quantity decreases  
  - pills start off at 1.  every pill eaten increases by 1 point.  
- return {ateAPill, newLocation} from /move
- once all pills are eaten, it becomes a kill match
- respawn pills on a random interval at a random location for a random point value
- when attcking, the each player's health is reduced by the other play's health.  
- If you health ever becomes <= 0, you die.
- when you die, you drop a pill worth 50% on the cell you just vacated
- add a /board endpoint returning state of board [{location, pillValue, playerId}]

#Bugs
- Table shrinks when no pills in a row/column, it should stay the same size.
- 

#Premise
- Eat pills
- Once all the *initial* pills are gone, battle mode commences 

#Tournament
- One round of p v p
- One round of single automated vs single automated
- One(?) round battle royale
