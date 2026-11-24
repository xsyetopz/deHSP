	var_0 = 3
	goto * label_1
*label_1
#module "counter"
#deffunc tick int, int, int, int
	var_1 = var_1 + var_0@
	return
#deffunc show int, int, int, int
	mes "total " + var_1
	return
#global
#module
#deffunc greet str
	mes "hi"
	return
#global
	tick@counter
	tick@counter
	show@counter
	greet@m0 "a"
	goto label_7
*label_7
	stop
