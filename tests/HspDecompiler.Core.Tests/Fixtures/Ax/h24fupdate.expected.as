	var_0 = "abcdef"
	var_1 = ""
	strmid var_1, var_0, 1, 3
	instr var_2, var_0, "cd"
	repeat 3
		if cnt = 1 : continue
		mes var_1
	loop
	keysend 0
	mes exedir
	stop
