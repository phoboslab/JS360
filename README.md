JS360
==========

A NET framework to enable use of JavaScript on the XBox with XNA, based on [Jurassic](http://jurassic.codeplex.com/). Jurassic was modified to compile JavaScript code into a NET assembly, allowing it to run on the XBox' Compact Framework.

It's pretty hack-ish.

In Windows debug, the project just loads the `Game/index.js` and starts the game. The XBox build however calls the windows build during compile with a set of parameters to generate an Assembly from the JavaScript source.

	JS360.exe /generate in.js out.dll
	
The XBox build then just calls into the `Generated.Main()` method from the Assembly when starting the game.

Initially this project aimed to mimic the HTML5 Canvas and Audio element to allow HTML5 games to run on the XBox, but this turned out to be much too slow. The Canvas class is very basic and only implements the `drawImage()` and `drawRect()` methods. It's very very far from complete.

The included `Game/index.js` draws a set of sprites on the screen. This runs smoothly on the XBox with up to ~300 sprites. Things seem to get very slow when you do some basic computations in JS.

More info in my blog:
http://www.phoboslab.org/log/2012/04/javascript-on-the-xbox-360

Jurassic is licensed under Ms-PL (see `Jurassic/License.txt`), the rest is under MIT License.