BDNA-12055:
[J.P. Morgan Chase & Co.][Fingerprints] Need to add windows fingerprint for product named "FlexLM License Server"

FlexNet Publisher (formerly known as FLEXlm) is a software license manager from Flexera Software which implements license management and is intended to be used in corporate environments to provide floating licenses to multiple end users of computer software.
FlexNet Publisher - Wikipedia
https://en.wikipedia.org/wiki/FlexNet_Publisher


Download FlexNet Publisher:
https://developer.arm.com/products/software-development-tools/license-management/downloads


armlmd.exe
armlmdiag.exe
lmgrd.exe
lmtools.exe
lmutil.exe
fnp_LicAdmin.pdf
ReleaseNotes.pdf


license.dat:
SERVER Viinfamiet 94de800aea94 27000
VENDOR armlmd C:\flexlm\armlmd.exe

# QCKVU3 GDSII (FULL)
FEATURE ACS58IO artwork 1.0 20-oct-2020 1 SIGN="003F 66A8 66AA B956 \
1849 9979 63A2 E500 5BF0 0C77 5A5B D17A DB80 E740 6913"

# QCKVU3 OASIS (FULL)
FEATURE ACS583O artwork 1.0 20-oct-2020 1 SIGN="002C B06B C0CD F1AB \
0D8E 3785 8998 F900 4146 5059 088D 7D24 E127 9F7A 543E"



Configuration:

  Uncompress the package BX002-PT-00005-r11p14-00rel0.zip (win 64bit), BX002-PT-00007-r11p14-00rel0.tgz (linux64). On windows, run lmtools.exe, Config Services:

Service Name:
Path to the Imgrd.exe file: C:\flexlm\lmgrd.exe
Path to the license file: C:\flexlm\license.dat
Path to the debug log file: C:\ProgramData\FNP_DIR\debug.log


Then
1. click Save Service -> start server--> you will see process "lmgrd", but you can't get the path of lmgrd, that's to say you can't write a fp based on this process.
2. click Save Service + tick 'Use Services' -> start server--> you will see a new service on services.msc, you can get the path from the service, so, you can write a fp based on this services.
3. at present, I can't start the server through service, proabably because of the fake license, if customer set it as service, we can't provide them a new fp based on service.



 

