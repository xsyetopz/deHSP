	a=300
	b=70000
	repeat 3
		if cnt=1 : mes "one" : else : mes "other"
	loop
*top
	if a=300 : if b=70000 : mes "both"
	if a=1 : goto *top
	mes {"
line 01 of a text longer than one line can hold
line 02 of a text longer than one line can hold
line 03 of a text longer than one line can hold
line 04 of a text longer than one line can hold
line 05 of a text longer than one line can hold
line 06 of a text longer than one line can hold
"}
	stop
