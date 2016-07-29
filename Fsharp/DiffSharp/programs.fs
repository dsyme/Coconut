﻿[<ReflectedDefinition>]
module programs

open linalg
open types
open corelang

let test1 (x: Number): Number = 
  let a = 1. / x
  a / (a + 1.)

let test2 (x: Number) (b: Number): Number =
  (x + b) - b

let vector_add3 (v1: Vector) (v2: Vector) (v3: Vector): Vector = 
  add_vec v1 (add_vec v2 v3)

let matrix_add3 (m1: Matrix) (m2: Matrix) (m3: Matrix): Matrix = 
  matrixAdd m1 (matrixAdd m2 m3)

let small_tests(dum: Number) = 
  let num = 2.
  let a = test1(num)
  numberPrint a
  numberPrint (test2 num a)
  ()