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
(*
let array_slice (v: 'a array) i j = v.[i..j]
let array_slice_s (storage: Storage) (v: Vector) i j = vectorBuildGivenStorage storage (fun k -> v.[i+k])
*)
[<DontOptimize>]
let hoistingExample (v: Vector) = 
  let sum = 
    iterateNumber (fun acc idx ->
        let tmp = v.[idx..(idx+9)]  // First, let's desugar this to GetArraySlice/array_slice as F# does...
        acc + sqnorm (add_vec tmp tmp)
    ) 0. 0 9
  numberPrint sum
  ()

(*                                                                            
let hoistingExample_pass1 (v: Vector) =                                   
  let sum =                                                               
    iterateNumber (fun acc idx ->                                         
        let tmp = array_slice v idx (idx+9)      // now rewrite...        
        acc + sqnorm (add_vec tmp tmp)                             
    ) 0. 0 9                                                       
  numberPrint sum                                                  
  ()                                                               
         
// Rule:      
//   let %v = %f %%args in %e
// 
// when !isscalar(%v)
// rewrites_to
//
//   vectorAllocCPS (sizeof (%f %%args))  /* sizeof to be optimized separately using various sizeof rules */  
//                  (fun storage -> let v = %f_s storage %%args in %e)      
//                
                                                                 
let hoistingExample_pass2 (v: Vector) =                                
  let sum =                                                        
    iterateNumber (fun acc idx ->                                  
        vectorAllocCPS 10 (fun storage ->                          
          let tmp = array_slice_s storage v idx (idx+9)
          acc + sqnorm (add_vec tmp tmp)
        )
    ) 0. 0 9
  numberPrint sum
  ()

let hoistingExample_pass3 (v: Vector) =                     
  let sum =                                                
    vectorAllocCPS 10 (fun storage ->                  
       iterateNumber (fun acc idx ->                          
          let tmp = array_slice_s storage v idx (idx+9)   // tmp is an alias of storage -- need to reason about tmp before we can do this swap?
          acc + sqnorm (add_vec tmp tmp)
        ) 0. 0 9
    )
  numberPrint sum
  ()

*)

[<DontOptimize>]
let explicitMallocExample1(v: Vector) = 
  //let storage1 = vectorAlloc 10 
  vectorAllocCPS 10 (fun storage1 ->
    let sum = 
      iterateNumber (fun acc idx ->
          let tmp = vectorBuildGivenStorage storage1 (fun i -> v.[i + idx])
          sqnorm (add_vec tmp tmp)
        ) 0. 0 9
    numberPrint sum
  )
  ()

[<DontOptimize>]
let explicitMallocExample2 (v: Vector) = 
  vectorAllocCPS 10 (fun storage1 ->
    vectorAllocCPS 10 (fun storage2 -> 
      let sum = 
        iterateNumber (fun acc idx ->
            let tmp = vectorBuildGivenStorage storage1 (fun i -> v.[i + idx])
            let tmp2 = add_vecGivenStorage storage2 tmp tmp
            sqnorm tmp2
          ) 0. 0 9
      numberPrint sum
    )
  )
  ()

let small_tests (dum: Number) = 
  let num = 2.
  let a = test1(num)
  numberPrint a
  numberPrint (test2 num a)
  let v2 = vectorRange(0)(99)
  hoistingExample(v2)
  explicitMallocExample1(v2)
  explicitMallocExample2(v2)
  ()