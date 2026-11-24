	define d40 40 : d40=3
	v0=1
	v1=v0*2+d40
	if v0=1 : mes "one" : else : mes "other"
	repeat 3
	if cnt=1 : break
	mes "count"
	loop
	gosub *l0
	pos 10,10
	button "GO",*l1
	mes ""
		"piece00 of a long message, piece01 of a long message, piece02 of a long message, piece03 of a long message, piece04 of a long message, piece05 of a long message, piece06 of a long message, piece07 of a long message, piece08 of a long message, piece09"+
		" of a long message, "+
	mes "tab\tquote\"
		"rest"+
	stop
*l1
	mes "a\nb"
	end
*l0
	mes "“ú–{Œê‚Ì•¶Žš—ñ"
	return
