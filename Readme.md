# Improvements
- [x] Increase point value of pills as remaining quantity decreases  
  - [x] pills start off at 1.  every pill eaten increases by 1 point.  
- [x] return {ateAPill, newLocation} from /move
- [x] once all pills are eaten, it changes to battle mode
  - [x] when attcking, the each player's health is reduced by the other play's health.  
  - [x] If you health ever becomes <= 0, you die.
  - [x] when you die, you drop a pill worth 50% on the cell you just vacated
- [ ] respawn pills on a random interval at a random location for a random point value
- [x] add a /board endpoint returning state of board [{location, pillValue, playerId}]
- [ ] Add an automatic game timer that continually restarts the game every x minutes
- [ ] Add logic to automatically boot any players that didn't move during the last game

# Bugs
- ☑ Table shrinks when no pills in a row/column, it should stay the same size.
- 

# Premise
- Eat pills
- Once all the *initial* pills are gone, battle mode commences until 1 player left standing.

# Tournament
- One round of p v p
- One round of single automated vs single automated
- One(?) round battle royale, no holds barred - you program it, you run it (max 64 clients per contestant)
