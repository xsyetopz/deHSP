	a=300
	b=70000
	c=-5
	repeat 3
		if cnt=1 : continue
		if cnt=2 : break
		mes "n"+cnt
	loop
	gosub *sub
	stop
*sub
	mes "x"
	return
