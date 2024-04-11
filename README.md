### 说明
这是一个把小雅元数据解压到指定文件夹的命令行工具，需要 .net 8 运行时。

### 使用方法
下载最新[Release](https://github.com/zhouguangjie/XiaoyaMetaSync/releases/latest)，解压，写批处理或使用计划任务调用。  
命令格式：`.\XiaoyaMetaSync.exe 功能 <必要参数>[可选参数]`  
每次调用都会生成日志，路径：`%LOCALAPPDATA%\XiaoyaMetaSync\Log`  

#### 导出元数据到指定路径：--sync
`.\XiaoyaMetaSync.exe --sync <小雅元数据压缩包文件路径> <元数据输出路径> [--ignore <忽略路径列表文件>] [-R <查找1> <替换1>] [-R <查找2> <替换2>]...`  
  
可选参数说明：
`--replace|-R <查找1> <替换1>`: 替换strm里的文本，该参数在其他命令里功能一样  
`--ignore <忽略路径列表文件>`: 指定一个列表文本，每行是压缩包里的一个相对路径，解压时会忽略这个路径下的所有文件  

例如以下命令是把下载的元数据all.mp4导出到Y:\all文件夹，并替换小雅alist地址：  
`.\XiaoyaMetaSync.exe --sync "D:\Downloads\all.mp4" "Y:\all" -R "http://xiaoya.host:5678" "http://istoreos:5688"`

#### 替换已经存在的strm文件：--strm
`.\XiaoyaMetaSync.exe --strm <元数据路径> [-R <查找1> <替换1>] [-R <查找2> <替换2>]...`

例如以下命令是把Y:\all文件夹和子文件夹所有的strm文件替换小雅alist地址：  
`.\XiaoyaMetaSync.exe --strm "Y:\all" -R "http://xiaoya.host:5678" "http://istoreos:5688"`

#### 复制元数据并为媒体文件生成strm：--genstrm
`.\XiaoyaMetaSync.exe --genstrm <alist挂载到本地的路径> <alist对应的url前缀> <输出路径> [--only_strm] [--rewrite_meta] [--rewrite_strm] [--encode_url]`  

例如为每日更新的所有视频文件生成strm：   
`.\XiaoyaMetaSync.exe --genstrm "Z:\xiaoya\每日更新" "http://istoreos:5688/d/每日更新" "D:\xiaoya_meta\meta_sync\每日更新" --only_strm`  

可选参数说明：  
`--only_strm`：只为视频文件生成strm，不复制其他元数据等文件  
`--rewrite_meta`：覆盖输出路径已经存在的元数据文件  
`--rewrite_strm`：覆盖输出路径已经存在strm文件  
`--encode_url`：对生成的strm文件的url进行编码  

#### 删除不存在于压缩包里的元数据文件夹：--remove_expired_meta
`.\XiaoyaMetaSync.exe --remove_expired_meta <小雅元数据压缩包文件路径> <元数据路径>`  

当新版压缩包某个文件夹已经删除或改名，用本方法可以删除元数据里的多余文件夹

#### 清理日志：--clear_log
日志路径：`%LOCALAPPDATA%\XiaoyaMetaSync\Log`  