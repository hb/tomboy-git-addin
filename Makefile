TOMBOY_DIR=$(HOME)/.config/tomboy/addins

GitNoteAddin.dll: GitNoteAddin.cs Git.addin.xml
	gmcs -debug -out:Git.dll -target:library -pkg:tomboy-addins -r:Mono.Posix GitNoteAddin.cs -resource:Git.addin.xml -resource:git.png

install: GitNoteAddin.dll
	mkdir -p $(TOMBOY_DIR)
	cp Git.dll $(TOMBOY_DIR)

uninstall:
	rm -f $(TOMBOY_DIR)/Git.dll

clean:
	rm -f Git.dll Git.mdb
