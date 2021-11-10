param(
    [Parameter(Mandatory = $true, HelpMessage = "host and port of server")]    
    [string]$url,
    [Parameter(Mandatory = $true, HelpMessage = "your player name")]    
    [string]$name
)

$token = iwr "$url/join?userName=$name"

read-host -Prompt "Press enter once the game has started."

$size = 0

while ($true) {
    $size = $size + 1

    write-host "moving right $size"
    for ($i = 0; $i -lt $size; $i++) {
        iwr "$url/move/right?token=$token" | out-null
        start-sleep -Milliseconds 250
    }

    write-host "moving down $size"
    for ($i = 0; $i -lt $size; $i++) {
        iwr "$url/move/down?token=$token" | out-null
        start-sleep -Milliseconds 250
    }

    $size = $size + 1

    write-host "moving left $size"
    for ($i = 0; $i -lt $size; $i++) {
        iwr "$url/move/left?token=$token" | out-null
        start-sleep -Milliseconds 250
    }

    write-host "moving up $size"
    for ($i = 0; $i -lt $size; $i++) {
        iwr "$url/move/up?token=$token" | out-null
        start-sleep -Milliseconds 250
    }
}