
# Main dirs to clean out
# bin obj AppPackages

$start = "C:\Users\toomr\source\repos\SimpleEpubReader\SimpleEpubReader"
get-childitem $start\obj -recurse -filter "*.epub" | remove-item
get-childitem $start\bin -recurse -filter "*.epub" | remove-item