@echo off
setLocal EnableDelayedExpansion

test.exe

set argfile="../ba_instances/benchmark_list.txt"
:: Reset argfile if supplied.
if "%~1" neq "" set argfile="%~1"
:: Remove quotes
set argfile=!argfile:"=!

for /f "tokens=*" %%G in (%argfile%) do (
  call echo "Running %%G"
  for /L %%i in (1,1,10) do (
    call ba.exe "../ba_instances/%%G"
  )
)
