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
- [x] Add an automatic game timer that continually restarts the game every x minutes
- [x] Add logic to automatically boot any players that didn't move during the last game

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

# Improvements for next time
- [ ] Rate limit 10 actions / second (maybe something [like this](https://code-maze.com/aspnetcore-web-api-rate-limiting/)?)
- [ ] Fog of war, only return the 20 cells in every direction
- [ ] Remove the /board endpoint, you only get board status as a response from a valid action
- [ ] Not two separate rounds, but let people attack immediately
- [ ] Allow multiple occupants per cell
- [ ] Separate /move /eat and /attack commands
- [ ] Variable pill values
- [ ] Every call to /eat only gets you 1 point.  Deduct remaining value of pill by 1.  Allows for concurrent eating of a single pill by multiple players
- [ ] Everyone loses x points every y amount of time
- [ ] Add different power-ups 
    - invincibility for y seconds
    - speed mode - doubles your action rate
    - poison pills
    - super eater - every call to /eat gets 5x points for y seconds
- [ ] Add a coding challenge hall of fame on engineering.snow.edu, include winners for game of life, risk, hungry game v1 and add to that
