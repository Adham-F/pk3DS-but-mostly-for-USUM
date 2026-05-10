</p>

<h1 align="center">pk3DS (But Extremely Catered Towards USUM) </h1>

<br />

pk3DS is a ROM editor for all 3DS Pokémon games that utilizes a variety of tools developed by a large group of contributors. pk3DS was created 
using C# and primarily focuses on its randomizer to provide users with a fresh and new experience in the beloved Pokémon games. 

This is a fork of pk3DS that is extremely catered towards USUM. It has a lot of features that the original pk3DS does not have to support and foster a better environment for hacking the games. Full credits to the original developers, and to ABZB for his ARM research.

## Table of contents

- [Features](#features)
- [Installation](#installation)
- [Usage](#usage)
- [Support](#support)

## Features

Our editor features a vast variety of randomizers to make every run as unique as possible. The Randomizers currently available are:

- Trainer Battles (Pokemon / Items / Moves / Abilities / Difficulty / Classes)
- Wild Encounters (Species, Level, Gen/Legend Specific, ORAS DexNav won't crash!)
- Personal Data (Pokemon Types / Stats / Abilities / TM Learnset)
- Move Randomizer (Type / Damage Category)
- Move Learnset (Level Up / Egg Move)
- Evolutions
- TM Moves
- Special Mart Inventory

## Newer Features

Here are the newer features present in this specific fork of pk3DS:

# Main Menu and Color Modes
- The program is no longer in Light Mode only, with Dark and Grey Mode being the two options usable for now.
- However, there are gradients that can be chosen as well to mix up the style! So far, the mascots from XY, ORAS, SM, USUM, and others like Rayquaza, Deoxys, and Zygarde have their own respective gradients, and even have a little interactive sprite in the Main menu to play with!

## Game Text and Story Text
- Support for adding new lines, and removing new lines. Makes things much easier for adjusting custom and existing elements in the game.
- A New Move Handler button that adjusts the respective text files of the game to make room for new moves. This does only make the text of new moves exist in the files, but other parts of the program support move additions as well, and all work together to make them function.
- Everytime you press Shift in the "In-Game Text Visualizer", a new line (\n) is added to the text file. This is used to simplify the proccess of changing text without having to look back and forth at how to properly format things.
- Find Next / Find Before / Replace All are new functions that make finding things and replacing things easier. Replace All works best if you add a new move and want to change the name of it.

# Personal Stats 
- "Copy Moves" and "Paste Moves" buttons that allow you to copy and paste moves from one Pokemon's learnset in Personal Data (i.e. TMs and Tutor Moves) to another. 
- Visible stat changes and comparisons when editing a Pokemon's stats; note for this to function properly, you must load a vanilla file of either Ultra Sun or Ultra Moon first for the program to generate a .txt to reference as you change stats. This does come with a vanilla .txt of stat files, so if you want to replace that just delete the file and it'll generate a new one based on the personal data of your game's files.
- "Copy Set" and "Paste Set" buttons that allow you to copy and paste the entire first tab of personal data from one Pokemon to another; this is convinient when you want to add new forms to the game.
- "Set Catch" and "Set Hatch" functions to make Pokemon always be caught instantly (255) for the former, and for making it hatch instantly for the latter (0).
- "Jump to Level Up" and "Jump to Egg Moves" buttons for ease of editing movepools.
- The Form Insertion Tool has been added into the tab as well, it will work as it tells you in the program. 
- Pokedex Entries are readable at the bottom left of the Pokemon that you choose to edit!

# Level Up Moves and Egg Moves
- "Copy" and "Paste" functions to copy and paste moves from one Pokemon's learnset in Level Up Moves and Egg Moves to another.
- "Add Move" and "Remove Move" buttons to make things easier for editing.
- "Import TSV" and "Apply Modern Sets" buttons to make it easier to apply modern movepools to Pokemon; for both of them, you will need either a custom TSV file of your custom movepools or a TSV of modern movepools; for the latter, a Google Spreadsheet called [Multiversal Movepool (v. 2.1.0)](https://docs.google.com/spreadsheets/d/14E8juaMuYBsqHX_0HnxwDt7StoBHUVFW7z6KtW_a9yY/edit?gid=1412121923#gid=1412121923) is highly reccomended to use, as you can copy and paste a tab from the sheet into a .txt to have it completely functional.

# Wild Encounters
- "Import TSV" and "Export TSV" to convert the tables into TSV files, which can make it more convinient to edit in Google Sheets, along with Import / Export tables.
- A Version Exclusives tab has been made to show what isn't present or available in the game you edit.
- A "Fill" button has been made to fill in the SOS part of a Pokemon with what is in the first encounterable slot.

# Mega Evolutions
- Alternate forms are toggleable in the Mega Evolutions tab; this was done for new alternate forms that could be added, but you can do whatever you want with it!

# Trainers
- Revamped UI, changing most of the positioning while still maintaining its effectiveness.
- More adjustable stats tab, with Hidden Power configuration and Happiness configuration.
- "Master / Master All" buttons to make all of the trainers have the best AI in the game. This is best for difficulty hacks!
- Import / Export / Import Team buttons that go with Pokemon Showdown! for ease of adding teams without having to do much in the program.
- Max IVs All / Doubles All / PokeChange All for more difficulty options.

# Showdown Set Storage System
- A new function has been added that stores sets you import from Pokemon Showdown into the program itself. It's meant to be convinient for people to easily access sets they want to use for their game without having to go back and forth trying to think about what could be done. This function exists for both the Trainer Editor and the Maison Editor.

# Items
- Import / Export .txt of the item data in the game.
- Everytime you press Shift on the Item Editor for the description of an item, a new line (\n) is added to the text file. This is used to simplify the proccess of changing text without having to look back and forth at how to properly format things.

# Move Editor
- Revamped UI, catered towards editing moves to the best of the program's ability.
- A Pokemon Champions PP setting to make the PP of moves match Pokemon Champions, if you don't want to rely on PP Maxes for moves being the best they can be in game.
- "Add New Moveslot" button to support new moves being added; note that this is supposed to be used in tandem with the "New Move Handler" in the Game Text.
- Sync Animations / BSEQs are buttons that are related to animation, but probably don't need to be messed with unless you know what you're doing with them. For all new moves added, they have the animation of Pound.
- Export / Import .txt of moves.
- "Load Vanilla Baseline" and "Changes Log" go hand in hand; the Changes Log is a log of what you change about moves and the Vanilla Baseline, which comes from a vanilla GARC being uploaded, is what it goes off of.
- Everytime you press Shift on the Move Editor for the description of an move, a new line (\n) is added to the text file. This is used to simplify the proccess of changing text without having to look back and forth at how to properly format things.

# Battle Royale / Tree
- Import / Export Showdown sets for ease of adding sets. Even comes with an Import Box option to just get a lot more sets added at once!
- Dump / Import PKMs if you would like to edit the Pokemon in a .txt file.
- A "Set List" option for Pokemon you want to set to a trainer; you will have to type in each Pokemon you want, so have a list ready!

# OWSE
- Added [Stracker's version of OWSE](https://github.com/Strackeror/pk3DS/releases/tag/stracker-1.0) to have Items and more editable.

# TMs
- An Update Description button has been added to update the description of a TM that has the move it teaches changed.
- An Export / Import .txt option if you want to edit in a .txt file instead.
- If you expanded the amount of TMs to 128, it can now edit them! But you must have the patch for it applied to your game's files in order for it to work.
- You can edit the item ID of your TMs, this is useful for adding new ones. However, do note that **adding them through the editor as of right now does not work.** Use the Research Center to do so.

# Type Chart
- Has sprites of types shown across the column and row of the chart, with numberical representations of interactions as well.

# Poke Mart and Move Tutor
- Both of these are lumped together as they both had the same function added to them; an Add and Delete button.
- The Move Tutor bug that was present in the old pk3DS does not exist anymore, meaning that if you change a tutorable move into a new move, it will work!
- Adding new items to Marts and new moves to tutor tables now function completely!

# CRO Expander
- An Expander for .cro files; this is only for if you want to do more advanced hacking of your game. For more information, please join the [3DS Hacking Discord](https://discord.gg/UzNWRRFdRC)!

# Research Center
- Pretty much the holy grail of this version of pk3DS. You can expand Moves, Abilities, and Items, add new functions to your code.bin, battle.cro, shop.cro, etc., and do whatever you want as long as you have the necessary instructions to patch. For more information, please join the [3DS Hacking Discord](https://discord.gg/UzNWRRFdRC)!

## Installation

To download pk3DS, all you need to do is go into our [forum page](https://projectpokemon.org/home/forums/topic/34377-pk3ds-pok%C3%A9mon-3ds-rom-editor-and-randomizer/) and following the instructions there.

## Usage

To begin using pk3DS you must first download the pk3DS editor zip file. Once you've downloaded the zip file for the editor, dump your ROM from the 3DS Pokémon game of your choosing.

Place the files in the same folder then simply run the pk3DS.exe file.
Once you open up the executable you can begin having fun with our editor and randomizing all the attributes and characteristics of the game to your liking.
Below are some images of how the editor should look when you run it.
![RomFS Editing Tools](https://i.imgur.com/PEbkjld.png)
![ExeFS Editing Tools](https://i.imgur.com/LYd2am9.png)
![CRO Editing Tools](https://i.imgur.com/q14xfum.png)

## Support

If any bugs or errors are caught or experienced come to our [forum page](https://projectpokemon.org/home/forums/topic/34377-pk3ds-pok%C3%A9mon-3ds-rom-editor-and-randomizer/) and communicate with us on what the issue is.
Many community members as well as contributors are active and can be found there. 

# CREDITS
- Kaphotics for pk3DS
- ABZB for ARM research, along with massive helpfulness related to editing .cro files
- [InfinityPlus05](https://www.pokecommunity.com/threads/averaged-movesets-for-pokemon-across-generations.530142/) for the Multiversal Movepool
- Stracker for his [OWSE editor](https://github.com/Strackeror/pk3DS/releases/tag/stracker-1.0)
