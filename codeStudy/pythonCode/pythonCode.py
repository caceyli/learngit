#create a new file 'test,txt', and write 2 lines into that file. If that file alreadey existed, it will be covered.
def main():
	f=open("test.txt",'w+')
	f.writelines(['dd\n','ee'])
	f.close()

if __name__ == '__main__':
	main()


# read every line from file 'test.txt' and out put all lines on screen.
def main():
	f=open("test.txt")
	line=f.readline()
	while line:
		print line,                # with ',' to ignore '\n' for python 2.X
		#print(line, end = '')     # ignore '\n' for python 3.X
		line=f.readline()
	f.close()

if __name__ == '__main__':
	main()

