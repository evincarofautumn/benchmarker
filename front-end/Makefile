all : build/main.js build/style.css

build/main.js : src/main.js Makefile
	jstransform --strip-types --react --harmony src/ build/

build/style.css : src/style.css Makefile
	cp -f src/style.css build/style.css
