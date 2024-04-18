import os
import subprocess
import time
import shutil
import re

#'''
fn_list = [
    #'tf2_ssd_mobilenet_v2_coco17_ptq',
    #'ssd_mobilenet_v2_coco_quant_postprocess',
    #'ssdlite_mobiledet_coco_qat_postprocess',
    #'ssd_mobilenet_v1_coco_quant_postprocess',
    #'tf2_ssd_mobilenet_v1_fpn_640x640_coco17_ptq',
    #'efficientdet_lite0_320_ptq',
    #'efficientdet_lite1_384_ptq',
    #'efficientdet_lite2_448_ptq',
    #'efficientdet_lite3_512_ptq',
    #'efficientdet_lite3x_640_ptq',
    #'yolov5n-int8',
    #'yolov5s-int8',
    #'yolov5m-int8',
    #'yolov5l-int8',
    #'yolov8n_416_640px', # lg 1st seg
    #'yolov8s_416_640px', # lg 1st seg
    #'yolov8m_416_640px', # lg 1st seg
    #'yolov8l_416_640px', # lg 1st seg
    #'yolov8n_640px',
    #'yolov8s_640px',
    #'yolov8m_640px', # lg 1st seg
    #'yolov8l_640px', # lg 1st seg
    'ipcam-general-v8']

custom_args = {
    'tf2_ssd_mobilenet_v2_coco17_ptq': {
        2: ["--diff_threshold_ns","100000"]},
    'ssd_mobilenet_v2_coco_quant_postprocess': {
        5: ["--undefok=enable_multiple_subgraphs","--enable_multiple_subgraphs","--partition_search_step","3"]},
    'ssdlite_mobiledet_coco_qat_postprocess': {
        2: ["--diff_threshold_ns","100000"]},
    'efficientdet_lite3_512_ptq': {
        2: ["--undefok=enable_multiple_subgraphs","--enable_multiple_subgraphs"],
        3: ["--undefok=enable_multiple_subgraphs","--enable_multiple_subgraphs"],
        4: ["--undefok=enable_multiple_subgraphs","--enable_multiple_subgraphs"],
        5: ["--undefok=enable_multiple_subgraphs","--enable_multiple_subgraphs"],
        6: ["--undefok=enable_multiple_subgraphs","--enable_multiple_subgraphs"],
        7: ["--undefok=enable_multiple_subgraphs","--enable_multiple_subgraphs"]},
    'efficientdet_lite3x_640_ptq': {
        5: ["--undefok=enable_multiple_subgraphs","--enable_multiple_subgraphs","--partition_search_step","2"],
        6: ["--undefok=enable_multiple_subgraphs","--enable_multiple_subgraphs","--partition_search_step","3"]},
    'yolov5n-int8': {
        5: ["--partition_search_step","2"],
        6: ["--partition_search_step","2"],
        7: ["--partition_search_step","2"],
        8: ["--partition_search_step","2"]},
    'yolov5s-int8': {
        5: ["--partition_search_step","2"],
        6: ["--partition_search_step","2"],
        7: ["--partition_search_step","2"],
        8: ["--partition_search_step","2"]},
    'yolov5m-int8': {
        5: ["--partition_search_step","2"],
        6: ["--partition_search_step","2"],
        7: ["--partition_search_step","2"],
        8: ["--partition_search_step","2"]},
    'yolov5l-int8': {
        5: ["--undefok=enable_multiple_subgraphs","--enable_multiple_subgraphs","--partition_search_step","2"],
        6: ["--partition_search_step","2"],
        7: ["--partition_search_step","2"],
        8: ["--partition_search_step","2"]},
    'yolov8m_416_640px': {
        5: ["--partition_search_step","2"],
        6: ["--partition_search_step","3"],
        7: ["--partition_search_step","4"],
        8: ["--partition_search_step","5"]},
    'yolov8l_416_640px': {
        4: ["--partition_search_step","2"],
        5: ["--partition_search_step","2"],
        6: ["--partition_search_step","3"],
        7: ["--partition_search_step","4"],
        8: ["--partition_search_step","5"]}}#'''

'''
fn_list = [
#    'yolov5n-int8',
#    'yolov5s-int8',
#    'yolov5m-int8',
#    'yolov5l-int8',
#    'yolov8n_full_integer_quant',
#    'yolov8s_full_integer_quant',
#    'yolov8m_full_integer_quant',
#    'yolov8l_full_integer_quant',
#    'yolov8n_480px',
#    'yolov8s_480px',
#    'yolov8m_480px',
#    'yolov8l_480px',
#    'yolov8n_512px',
#    'yolov8s_512px',
#    'yolov8m_512px',
#    'yolov8l_512px',
#    'yolov8s_544px',
#    'yolov8m_544px', # lg 1st seg
#    'yolov8l_544px', # lg 1st seg
#    'yolov8s_576px',
#    'yolov8m_576px', # lg 1st seg
#    'yolov8l_576px', # lg 1st seg
#    'yolov8s_608px',
#    'yolov8m_608px', # lg 1st seg
#    'yolov8l_608px',
#    'yolov8n_640px',
#    'yolov8s_640px',
#    'yolov8m_640px', # lg 1st seg
#    'yolov8l_640px', # lg 1st seg
#    'yolov8n_416_640px', # lg 1st seg
    'yolov8s_416_640px', # lg 1st seg
    'yolov8m_416_640px', # lg 1st seg
    'yolov8l_416_640px'] # lg 1st seg
#    'ipcam-general-v8'] #'''
   
'''
custom_args = {
    'yolov8n_full_integer_quant': {
        2: ["--diff_threshold_ns","100000"],
        3: ["--diff_threshold_ns","200000"]},
    'yolov8s_full_integer_quant': {
        2: ["--diff_threshold_ns","200000"]},
    'yolov8l_full_integer_quant': {
        5: ["--partition_search_step","2"]},
    'yolov8n_480px': {
        2: ["--diff_threshold_ns","100000"],
        3: ["--diff_threshold_ns","200000"]},
    'yolov8s_480px': {
        2: ["--diff_threshold_ns","200000"]},
    'yolov8m_480px': {
        5: ["--partition_search_step","2"]},
    'yolov8n_512px': {
        2: ["--diff_threshold_ns","1200000"],
        3: ["--diff_threshold_ns","600000"]},
    'yolov8s_512px': {
        2: ["--diff_threshold_ns","200000"]},
    'yolov8m_640px': {
        2: ["--diff_threshold_ns","200000", "--undefok=timeout_sec","--timeout_sec=360"]},
    'yolov8l_640px': {
        2: ["--undefok=timeout_sec","--timeout_sec=360"]},
    'yolov8n_416_640px': {
        5: ["--partition_search_step","2"]},
    'yolov8s_416_640px': {
        5: ["--partition_search_step","2"]},
    'yolov8m_416_640px': {
        5: ["--initial_lower_bound_ns","44658311","--initial_upper_bound_ns","45466138","--partition_search_step","2"],
        6: ["--initial_lower_bound_ns","39444004","--initial_upper_bound_ns","40071927","--partition_search_step","3"],
        7: ["--initial_lower_bound_ns","36028652","--initial_upper_bound_ns","37012866","--partition_search_step","4"],
        8: ["--initial_lower_bound_ns","33892323","--initial_upper_bound_ns","34856571","--partition_search_step","5"]},
    'yolov8l_416_640px': {
        5: ["--initial_lower_bound_ns","82297482","--initial_upper_bound_ns","82892528","--partition_search_step","2"],
        6: ["--initial_lower_bound_ns","69966647","--initial_upper_bound_ns","70757195","--partition_search_step","3"],
        7: ["--initial_lower_bound_ns","69067450","--initial_upper_bound_ns","69599451","--partition_search_step","4"],
        8: ["--initial_lower_bound_ns","55889854","--initial_upper_bound_ns","56444625","--partition_search_step","5"]}}#'''

'''
diff_threshold_ns = {
    'yolov8s_416_640px': {
        2: 4000000},
    'yolov8m_416_640px': {
        4: 40000000,
        5: 30000000},
    'yolov8l_416_640px': {
        7: 90000000,
        8: 70000000}}#'''

'''
custom_args = {
    'yolov8m_416_640px': {
        5: ["--partition_search_step","2"],
        6: ["--partition_search_step","3"],
        7: ["--partition_search_step","4"],
        8: ["--partition_search_step","5"]},
    'yolov8l_416_640px': {
        4: ["--partition_search_step","2"],
        5: ["--partition_search_step","2"],
        6: ["--partition_search_step","3"],
        7: ["--partition_search_step","4"],
        8: ["--partition_search_step","5"]}}#'''
   
seg_dir = "/media/seth/FAT_THUMB/all_segments/"
seg_types = ['', '2x_first_seg/', '15x_first_seg/', '166x_first_seg/', '3x_first_seg/', '4x_first_seg/', 'inc_seg/', 'dumb/']


def seg_exists(filename, segment_type, segment_count):
    if segment_type == 'orig_code':
        segment_type = ''

    if segment_count == 1:
        seg_list = [seg_dir+segment_type+filename+'_edgetpu.tflite']
    else:
        seg_list = [seg_dir+segment_type+filename+'_segment_{}_of_{}_edgetpu.tflite'.format(i, segment_count) for i in range(segment_count)]
    return (seg_list, any([True for s in seg_list if not os.path.exists(s)]))

MAX_TPU_COUNT = 4

'''
# Generate segment files
for sn in range(1,MAX_TPU_COUNT+1):
    for fn in fn_list:
        for seg_type in seg_types:
            seg_list, file_missing = seg_exists(fn, seg_type, sn)

            if not file_missing:
                continue
               
            if sn == 1:
                cmd = ["/usr/bin/edgetpu_compiler","-s","-d","--out_dir",seg_dir+seg_type,seg_dir+fn+".tflite"]
            elif 'dumb' in seg_type:
                cmd = ["/usr/bin/edgetpu_compiler","-s","-d","-n",str(sn),"--out_dir",seg_dir+seg_type,seg_dir+fn+".tflite"]
            elif 'saturated' in seg_type:
                try:
                    cmd = ["libcoral/out/k8/tools/partitioner/partition_with_profiling","--output_dir",seg_dir+seg_type,"--edgetpu_compiler_binary",
                           "/usr/bin/edgetpu_compiler","--model_path",seg_dir+fn+".tflite","--num_segments",str(sn),
                           "--diff_threshold_ns", str(diff_threshold_ns[fn][sn])]
                except:
                    # Note: "Saturated segments" is an attempt to load as much of the model as possible onto segments
                    # while ignoring the latency incurred by slower segments. We assume we'll be able to "speed up"
                    # these slower segments simply by running more copies of them. The faster segments ideally will
                    # be optimized to all run at roughly the same speed. Thus the overall inference throughput will
                    # be limited by how many multiples of the slowest segment we can run.
                    #
                    # diff_threshold_ns key entries only exist where we want to create "saturated segments". More would
                    # mean the model is too sparse across segments. We create saturated segments by adjusting the
                    # diff_threshold_ns until the compiler just starts pushing parameters off of the TPUs. Ideally
                    # this will result in one or two slow segments and the rest of the segments are roughly equally
                    # fast.
                    continue

            else:
                if '2x_first_seg' in seg_type:
                    #+++ b/coral/tools/partitioner/profiling_based_partitioner.cc
                    #@@ -190,6 +190,8 @@ int64_t ProfilingBasedPartitioner::PartitionCompileAndAnalyze(
                    #     latencies = std::get<2>(coral::BenchmarkPartitionedModel(
                    #         tmp_edgetpu_segment_paths, &edgetpu_contexts(), kNumInferences));
                    #+    latencies[0] /= 2;
                    #     if (kUseCache) {
                    #       for (int i = 0; i < num_segments_; ++i) {
                    #         segment_latency_cache_[{segment_starts[i], num_ops[i]}] = latencies[i];
                    #@@ -211,10 +213,11 @@ std::pair<int64_t, int64_t> ProfilingBasedPartitioner::GetBounds(
                    #                      num_segments_, /*search_delegate=*/true,
                    #                      delegate_search_step))
                    #       << "Can not compile initial partition.";
                    #-  const auto latencies = std::get<2>(coral::BenchmarkPartitionedModel(
                    #+  auto latencies = std::get<2>(coral::BenchmarkPartitionedModel(
                    #       tmp_edgetpu_segment_paths, &edgetpu_contexts(), kNumInferences));
                    # 
                    #   DeleteFolder(tmp_dir);
                    #+  latencies[0] /= 4;
                    # 
                    #   int64_t lower_bound = std::numeric_limits<int64_t>::max(), upper_bound = 0;
                    #   for (auto latency : latencies) {
                    #
                    # sudo make DOCKER_IMAGE="ubuntu:20.04" DOCKER_CPUS="k8" DOCKER_TARGETS="tools" docker-build

                    #// Encourage each segment slower than the previous to spread out the bottlenecks
                    #double latency_adjust = 1.0;
                    #for (int i = 1; i < num_segments_; ++i)
                    #{
                    #  if (latencies[i-1] < latencies[i])
                    #    latency_adjust *= 0.97;
                    #  latencies[i-1] *= latency_adjust;
                    #}
                    #latencies[num_segments_-1] *= latency_adjust;
                    
                    partition_with_profiling_dir = "libcoral/tools.2"
                elif '15x_first_seg' in seg_type:
                    partition_with_profiling_dir = "libcoral/tools.15"
                elif '133x_first_seg' in seg_type:
                    partition_with_profiling_dir = "libcoral/tools.133"
                elif '166x_first_seg' in seg_type:
                    partition_with_profiling_dir = "libcoral/tools.166"
                elif '3x_first_seg' in seg_type:
                    partition_with_profiling_dir = "libcoral/tools.3"
                elif '4x_first_seg' in seg_type:
                    partition_with_profiling_dir = "libcoral/tools.4"
                elif '15x_last_seg' in seg_type:
                    partition_with_profiling_dir = "libcoral/tools.last15"
                elif '2x_last_seg' in seg_type:
                    partition_with_profiling_dir = "libcoral/tools.last2"
                elif 'inc_seg' == seg_type:
                    partition_with_profiling_dir = "libcoral/tools.inc_seg"
                else:
                    partition_with_profiling_dir = "libcoral/tools.orig"

                cmd = [partition_with_profiling_dir+"/partitioner/partition_with_profiling","--output_dir",seg_dir+seg_type,"--edgetpu_compiler_binary",
                       "/usr/bin/edgetpu_compiler","--model_path",seg_dir+fn+".tflite","--num_segments",str(sn)]
           
                try:
                    cmd += custom_args[fn][sn]
                except:
                    pass
           
            print(cmd)
            subprocess.run(cmd)#'''
           

seg_types += ['133x_first_seg/', '15x_last_seg/', '2x_last_seg/']

# Test timings
fin_timings = {}
fin_fnames = {}
for fn in fn_list:
    timings = []
    fin_timings[fn] = {}
    fin_fnames[fn] = {}

    for num_tpus in range(2,MAX_TPU_COUNT+1):

        for seg_type in seg_types:
            max_seg = 0
            for sn in range(1,num_tpus+1):

                # Test against orig code
                exe_file = "/home/seth/CodeProject.AI-Server/src/modules/ObjectDetectionCoral/objectdetection_coral_multitpu.py"

                # Get file types
                seg_list, file_missing = seg_exists(fn, seg_type, sn)

                if file_missing:
                    continue
                max_seg = sn

                cmd = ["python3",exe_file,"--model"] + \
                      seg_list + ["--labels","coral/pycoral/test_data/coco_labels.txt","--input","/home/seth/coral/pycoral/test_data/grace_hopper.bmp",
                      "--count","2000","--num-tpus",str(num_tpus)]
                print(cmd)
                c = subprocess.run(cmd, check=True, universal_newlines=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
                print(c.stdout)
                print(c.stderr)
                ms_time = float(re.compile(r'threads; ([\d\.]+)ms ea').findall(c.stderr)[0])
                timings.append((ms_time, num_tpus, fn, seg_type, sn))

        timings = sorted(timings, key=lambda t: t[0])

        # Print the top three
        print(f"TIMINGS FOR {num_tpus} TPUs AND {fn} MODEL:")
        for t in range(min(10,len(timings))):
            print(timings[t])

        # Get best segments, but
        # Skip if it's not 'orig_code' and > 1 segment
        t = [t for t in timings if t[3] != 'orig_code'][0]
        if t[4] == 1:
            continue

        # Add segment to the final list 
        fin_timings[fn][num_tpus] = timings[0]

        # Copy best to local dir
        seg_list, _ = seg_exists(t[2], t[3], t[4])
        fin_fnames[fn][num_tpus] = []
        for s in seg_list:
            file_components = os.path.normpath(s).split("/")
            out_fname = file_components[-2]+"_"+file_components[-1]
            shutil.copyfile(s, out_fname)
            fin_fnames[fn][num_tpus].append(out_fname)

        # Create archive for this model / TPU count
        if any(fin_fnames[fn][num_tpus]):
            cmd = ['zip', '-9', f'objectdetection-{fn}-{num_tpus}-edgetpu.zip'] + fin_fnames[fn][num_tpus]
            print(cmd)
            subprocess.run(cmd)

print(fin_timings)
print(fin_fnames)
 
# Pretty print all of the segments we've timed and selected
for fn, v in fin_fnames.items():
    print("             '%s': {" % fn)
    for tpu_count, out_fnames in v.items():
        print(f"                 # {fin_timings[fn][tpu_count][0]:6.1f} ms per inference")
        print(f"                 {tpu_count}: "+str(out_fnames)+",")
    print("             },")
