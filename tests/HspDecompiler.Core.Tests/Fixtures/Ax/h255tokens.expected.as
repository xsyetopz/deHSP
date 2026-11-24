	var_0 = 300
	var_1 = 70000
	var_2 = $fffffffb
	repeat 3
		if cnt = 1 {
			continue
		}
		if cnt = 2 {
			break
		}
		mes "n" + cnt
	loop
	gosub * label_2
	stop
*label_2
	mes "x"
	return
