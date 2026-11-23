#undef mes
#deffunc mes str
	mref var_0, 32
	mes@hsp "> " + var_0
	return
*label_2
	var_1 = 0
	repeat 5
		if cnt = 1 {
			continue
		}
		if cnt = 4 {
			break
		}
		var_1 = var_1 + cnt
	loop
	mes "sum=" + var_1
*label_4
	stop
