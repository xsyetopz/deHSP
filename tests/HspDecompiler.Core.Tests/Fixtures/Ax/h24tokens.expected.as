	var_0 = 300
	var_1 = 70000
	repeat 3
		if cnt = 1 : mes "one" : else : mes "other"
	loop
*label_1
	if var_0 = 300 : if var_1 = 70000 : mes "both"
	if var_0 = 1 : goto * label_1
	mes {"line 01 of a text longer than one line can hold
line 02 of a text longer than one line can hold
line 03 of a text longer than one line can hold
line 04 of a text longer than one line can hold
line 05 of a text longer than one line can hold
line 06 of a text longer than one line can hold
"}
	stop
