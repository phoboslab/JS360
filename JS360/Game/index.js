var width = 1280;
var height = 720;
var numSprites = 20;

var canvas = new Canvas();
var ctx = canvas.getContext('2d');
var sprites = [];

var last = Date.now();
var tick = 0;


var Sprite = function( image ) {
	this.image = image;
	this.x = Math.random() * width;
	this.y = Math.random() * height;
	this.vx = Math.random() * 200 + 400;
	this.vy	= Math.random() * 100 + 300;
	
	this.draw = function() {
		this.x += tick * this.vx;
		this.y += tick * this.vy;
		
		if( this.y > height ) {
			this.x = Math.random() * width * 2 - width;
			this.y = -256;
		}
		
		ctx.drawImage( this.image, this.x, this.y );
	}
};

// Calculate the tick, clear the screen and draw the sprites
var draw = function() {
	var current = Date.now();
	tick = (current - last)/1000;
	last = current;
	
	ctx.fillRect( 0, 0, width, height );
	for( var i = 0; i < sprites.length; i++ ) {
		sprites[i].draw();
	}
}


// Load the image and start the draw interval
var comet = new Image();
comet.onload = function() {	
	setInterval( draw, 1000/60 );
	ctx.fillStyle = '#000000';
	
	for( var i = 0; i < numSprites; i++ ) {
		sprites.push( new Sprite(comet) );
	}
};

comet.src = 'impact-comet.png';
