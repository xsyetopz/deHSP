#undef mes
#deffunc mes str
	mref s, 32
	mes@hsp "> " + s
	return
*start
	a = 0
	repeat 5
		if cnt = 1 : continue
		if cnt = 4 : break
		a = a + cnt
	loop
	mes "sum=" + a
*unused
	stop
