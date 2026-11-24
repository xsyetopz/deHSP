count = 3
goto *main
*main
#module "Counter"
#deffunc tick
total = total + count@
return
#deffunc show
mes "total " + total
return
#global
#module
#deffunc greet str
mes "hi"
return
#global
tick
tick@counter
show
greet "a"
goto x1
*x1
stop
