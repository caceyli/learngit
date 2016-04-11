#!/usr/bin/perl
use strict;
use warnings;

#$sqlLine0 = "(select MIN(CAT_CREATE_DATE) FROM (SELECT COALESCE(CAT_HW_MODEL_FDA.CREATE_DATE, CONVERT(DATETIME, '20000101', 120)) AS CAT_CREATE_DATE union all SELECT COALESCE(CAT_FDA.CREATE_DATE, CONVERT(DATETIME, '20000101', 120)) AS CAT_CREATE_DATE ) CAT_CREATE_DATE) as CAT_CREATE_DATE,";

#$sqlLine = "(select Max(CAT_LAST_MODIFIED_DATE) FROM (SELECT COALESCE(CAT_HW_MODEL_FDA.LAST_MODIFIED_DATE, CONVERT(DATETIME, '20000101', 120)) AS CAT_LAST_MODIFIED_DATE union all SELECT COALESCE(CAT_FDA.LAST_MODIFIED_DATE, CONVERT(DATETIME, '20000101', 120)) AS CAT_LAST_MODIFIED_DATE ) CAT_LAST_MODIFIED_DATE) as CAT_LAST_MODIFIED_DATE";

while(<STDIN>) {
if ($_=~m/(min|max)/gi){
$fun = $1;
}
$fun = uc($fun);
%h=('MIN','LEAST','MAX','GREATEST');

$sqlLine = $_;
if ($sqlLine=~m/(min|max)\((.+?)\)/gi){
$colname = $2;
}

while($sqlLine=~m/(COALESCE.+?)\s+AS/g)
{
 push @colList,$1;
}

$oraLine = join (",",@colList);
printf "Trans SQL to ORA:\n";
printf "$h{$fun}\($oraLine\) AS $colname\n";
}
